using System.Text.Json;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BinomoBackend.Persistence.Redis;

public class RedisLimitOrderRepository : ILimitOrderRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RedisLimitOrderRepository> _logger;
    
    private const string PendingOrdersKey = "orders:pending"; // Hash
    private const string OrderKey = "order:"; // String per order
    private const string BuyOrdersPrefix = "orders:buy:"; // Sorted Set (score = limit price)
    private const string SellOrdersPrefix = "orders:sell:"; // Sorted Set (score = limit price)
    private const string UserOrdersPrefix = "orders:user:"; // Set per user

    public RedisLimitOrderRepository(
        IConnectionMultiplexer redis,
        ApplicationDbContext dbContext,
        ILogger<RedisLimitOrderRepository> logger)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SavePendingOrderAsync(LimitOrder order, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var orderJson = JsonSerializer.Serialize(order);

        var transaction = db.CreateTransaction();

        // 1. Сохраняем сам ордер
        var orderKey = $"{OrderKey}{order.Id}";
        transaction.StringSetAsync(orderKey, orderJson);

        // 2. Добавляем в Hash всех pending ордеров
        transaction.HashSetAsync(PendingOrdersKey, order.Id.ToString(), orderJson);

        // 3. Добавляем в индекс по пользователю
        var userKey = $"{UserOrdersPrefix}{order.UserId}";
        transaction.SetAddAsync(userKey, order.Id.ToString());

        // 4. 🔥 Sorted Set для быстрого поиска ордеров к исполнению
        // Score = limit price
        var sortedSetKey = order.Side == OrderSide.Buy
            ? $"{BuyOrdersPrefix}{order.Symbol}"
            : $"{SellOrdersPrefix}{order.Symbol}";

        transaction.SortedSetAddAsync(
            sortedSetKey,
            order.Id.ToString(),
            (double)order.LimitPrice);

        var success = await transaction.ExecuteAsync();

        if (!success)
        {
            _logger.LogError("Failed to save limit order {OrderId} to Redis", order.Id);
            throw new Exception("Redis transaction failed");
        }
    }

    public async Task<LimitOrder?> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var orderKey = $"{OrderKey}{orderId}";

        var json = await db.StringGetAsync(orderKey);

        if (json.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<LimitOrder>(json!);
    }

    public async Task<List<LimitOrder>> GetPendingOrdersBySymbolAsync(
        string symbol, 
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Получаем все ордера из обеих Sorted Sets
        var buyKey = $"{BuyOrdersPrefix}{symbol}";
        var sellKey = $"{SellOrdersPrefix}{symbol}";

        var buyOrderIds = await db.SortedSetRangeByRankAsync(buyKey);
        var sellOrderIds = await db.SortedSetRangeByRankAsync(sellKey);

        var orders = new List<LimitOrder>();

        foreach (var id in buyOrderIds.Concat(sellOrderIds))
        {
            var order = await GetOrderByIdAsync(Guid.Parse(id!), ct);
            if (order != null && order.Status == LimitOrderStatus.Pending)
            {
                orders.Add(order);
            }
        }

        return orders;
    }

    public async Task<List<LimitOrder>> GetUserPendingOrdersAsync(
        Guid userId, 
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var userKey = $"{UserOrdersPrefix}{userId}";

        var orderIds = await db.SetMembersAsync(userKey);

        var orders = new List<LimitOrder>();

        foreach (var id in orderIds)
        {
            var order = await GetOrderByIdAsync(Guid.Parse(id!), ct);
            if (order != null && order.Status == LimitOrderStatus.Pending)
            {
                orders.Add(order);
            }
        }

        return orders;
    }

    public async Task RemovePendingOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        var order = await GetOrderByIdAsync(orderId, ct);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for removal", orderId);
            return;
        }

        var transaction = db.CreateTransaction();

        // 1. Удаляем сам ордер
        var orderKey = $"{OrderKey}{orderId}";
        transaction.KeyDeleteAsync(orderKey);

        // 2. Удаляем из Hash
        transaction.HashDeleteAsync(PendingOrdersKey, orderId.ToString());

        // 3. Удаляем из индекса пользователя
        var userKey = $"{UserOrdersPrefix}{order.UserId}";
        transaction.SetRemoveAsync(userKey, orderId.ToString());

        // 4. Удаляем из Sorted Set
        var sortedSetKey = order.Side == OrderSide.Buy
            ? $"{BuyOrdersPrefix}{order.Symbol}"
            : $"{SellOrdersPrefix}{order.Symbol}";

        transaction.SortedSetRemoveAsync(sortedSetKey, orderId.ToString());

        var success = await transaction.ExecuteAsync();

        if (!success)
        {
            _logger.LogError("Failed to remove order {OrderId} from Redis", orderId);
            throw new Exception("Redis transaction failed");
        }

        _logger.LogInformation("✅ Order {OrderId} removed from Redis", orderId);
    }

    public async Task<List<LimitOrder>> GetOrdersForExecutionAsync(
        string symbol,
        decimal currentPrice,
        OrderSide side,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        string sortedSetKey;
        double min, max;

        if (side == OrderSide.Buy)
        {
            // Buy: исполнение если currentPrice <= limitPrice
            // "Куплю не дороже X" → исполняем когда цена упала до X или ниже
            sortedSetKey = $"{BuyOrdersPrefix}{symbol}";
            min = (double)currentPrice;
            max = double.PositiveInfinity;
        }
        else
        {
            // Sell: исполнение если currentPrice >= limitPrice
            // "Продам не дешевле X" → исполняем когда цена выросла до X или выше
            sortedSetKey = $"{SellOrdersPrefix}{symbol}";
            min = double.NegativeInfinity;
            max = (double)currentPrice;
        }

        // O(log n) range query
        var orderIds = await db.SortedSetRangeByScoreAsync(sortedSetKey, min, max);

        var orders = new List<LimitOrder>();

        foreach (var id in orderIds)
        {
            var order = await GetOrderByIdAsync(Guid.Parse(id!), ct);
            if (order != null && order.Status == LimitOrderStatus.Pending)
            {
                orders.Add(order);
            }
        }

        return orders;
    }
}