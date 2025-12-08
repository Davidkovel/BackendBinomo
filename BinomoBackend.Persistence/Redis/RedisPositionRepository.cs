using StackExchange.Redis;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Interfaces;
using BinomoBackend.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BinomoBackend.Persistence.Redis;

public class RedisPositionRepository : IRedisPositionRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RedisPositionRepository> _logger;

    // Redis Keys структура
    private const string ActivePositionsKey = "positions:active"; // Hash
    private const string PositionsBySymbolPrefix = "positions:symbol:"; // Sorted Set per symbol
    private const string PositionKey = "position:"; // String per position
    private const string LongPositionsPrefix = "positions:long:"; // Sorted Set (score = liq price)
    private const string ShortPositionsPrefix = "positions:short:"; // Sorted Set (score = liq price)

    public RedisPositionRepository(
        IConnectionMultiplexer redis,
        ApplicationDbContext dbContext,
        ILogger<RedisPositionRepository> logger)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
    }

    // ============= HOT DATA (Redis) =============

    public async Task<decimal?> GetPosititionProfit(Guid positionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var positionKey = $"{PositionKey}{positionId}";
        
        var json = await db.StringGetAsync(positionKey);

        if (json.IsNullOrEmpty)
        {
            return null;
        }
        
        var position = JsonSerializer.Deserialize<Position>(json);
        return position?.ProfitLoss;
    }

    public async Task SaveActivePositionAsync(Position position, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var positionJson = JsonSerializer.Serialize(position);

        var transaction = db.CreateTransaction();

        // 1. Сохраняем саму позицию
        var positionKey = $"{PositionKey}{position.Id}";
        transaction.StringSetAsync(positionKey, positionJson);

        // 2. Добавляем в Hash всех активных позиций
        transaction.HashSetAsync(ActivePositionsKey, position.Id.ToString(), positionJson);

        // 3. Индекс по символу (для быстрого поиска)
        var symbolKey = $"{PositionsBySymbolPrefix}{position.Symbol}";
        transaction.SetAddAsync(symbolKey, position.Id.ToString());

        // 4. Sorted Set для быстрого поиска позиций на ликвидацию
        // Score = liquidation price для O(log n) range queries
        var sortedSetKey = position.Type == PositionType.Long
            ? $"{LongPositionsPrefix}{position.Symbol}"
            : $"{ShortPositionsPrefix}{position.Symbol}";

        transaction.SortedSetAddAsync(
            sortedSetKey,
            position.Id.ToString(),
            (double)position.LiquidationPrice);

        // Commit транзакции
        var success = await transaction.ExecuteAsync();

        if (!success)
        {
            _logger.LogError("Failed to save position {PositionId} to Redis", position.Id);
            throw new Exception("Redis transaction failed");
        }
    }

    public async Task<Position?> GetActivePositionAsync(Guid positionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var positionKey = $"{PositionKey}{positionId}";

        var json = await db.StringGetAsync(positionKey);

        if (json.IsNullOrEmpty)
        {
            _logger.LogWarning("Position {PositionId} not found in Redis, checking DB", positionId);

            // Fallback на DB если не в Redis
            var position = await _dbContext.Positions.FindAsync(new object[] { positionId }, ct);

            if (position != null && position.Status == PositionStatus.Open)
            {
                // Cache miss - загружаем в Redis
                await SaveActivePositionAsync(position, ct);
            }

            return position;
        }

        return JsonSerializer.Deserialize<Position>(json!);
    }

    public async Task<List<Position>> GetActivePositionsBySymbolAsync(
        string symbol,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var symbolKey = $"{PositionsBySymbolPrefix}{symbol}";

        // O(1) получение всех ID позиций по символу
        var positionIds = await db.SetMembersAsync(symbolKey);

        var positions = new List<Position>();

        // Параллельная загрузка позиций
        var tasks = positionIds.Select(async id =>
        {
            var positionKey = $"{PositionKey}{id}";

            var json = await db.StringGetAsync(positionKey);

            if (!json.IsNullOrEmpty)
            {
                try
                {
                    var position = JsonConvert.DeserializeObject<Position>(json!);
                    return position;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "❌ Failed to deserialize position {PositionId} from Redis", id);
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("❌ No data found for position key: {PositionKey}", positionKey);
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);
        var validPositions = results.Where(p => p != null).ToList();
        positions.AddRange(validPositions);

        return positions;
    }

    public async Task<List<Position>> GetAllActivePositionsAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Получаем все позиции из Hash
        var allPositions = await db.HashGetAllAsync(ActivePositionsKey);

        var positions = new List<Position>();

        foreach (var entry in allPositions)
        {
            var position = JsonSerializer.Deserialize<Position>(entry.Value!);
            if (position != null)
            {
                positions.Add(position);
            }
        }

        return positions;
    }

    public async Task RemoveActivePositionAsync(Guid positionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        var position = await GetActivePositionAsync(positionId, ct);

        if (position == null)
        {
            _logger.LogWarning("Position {PositionId} not found for removal", positionId);
            return;
        }

        var transaction = db.CreateTransaction();

        // 1. Удаляем из основного хранилища
        var positionKey = $"{PositionKey}{positionId}";
        transaction.KeyDeleteAsync(positionKey);

        // 2. Удаляем из Hash
        transaction.HashDeleteAsync(ActivePositionsKey, positionId.ToString());

        // 3. Удаляем из индекса по символу
        var symbolKey = $"{PositionsBySymbolPrefix}{position.Symbol}";
        transaction.SetRemoveAsync(symbolKey, positionId.ToString());

        // 4. Удаляем из Sorted Set
        var sortedSetKey = position.Type == PositionType.Long
            ? $"{LongPositionsPrefix}{position.Symbol}"
            : $"{ShortPositionsPrefix}{position.Symbol}";

        transaction.SortedSetRemoveAsync(sortedSetKey, positionId.ToString());

        var success = await transaction.ExecuteAsync();

        if (!success)
        {
            _logger.LogError("Failed to remove position {PositionId} from Redis", positionId);
            throw new Exception("Redis transaction failed");
        }

        _logger.LogInformation("✅ Position {PositionId} removed from Redis", positionId);
    }

    // ============= СПЕЦИАЛЬНЫЕ МЕТОДЫ ДЛЯ ЛИКВИДАЦИИ =============

    public async Task<List<Position>> GetPositionsForLiquidationAsync(
        string symbol,
        decimal currentPrice,
        PositionType type,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        string sortedSetKey;
        double min, max;

        if (type == PositionType.Long)
        {
            sortedSetKey = $"{LongPositionsPrefix}{symbol}";
            min = double.NegativeInfinity;
            max = (double)currentPrice;
        }
        else
        {
            sortedSetKey = $"{ShortPositionsPrefix}{symbol}";
            min = (double)currentPrice;
            max = double.PositiveInfinity;
        }

        // O(log n) range query
        var positionIds = await db.SortedSetRangeByScoreAsync(sortedSetKey, min, max);

        var positions = new List<Position>();

        foreach (var id in positionIds)
        {
            var position = await GetActivePositionAsync(Guid.Parse(id!), ct);
            if (position != null)
            {
                positions.Add(position);
            }
        }

        return positions;
    }
}