using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Infrastructure.BackroundServices;

/// <summary>
/// Инициализирует партиционирование при первом запуске
/// Создает Partition Function и Partition Scheme если их нет
/// </summary>
public class PartitionInitializationService : IHostedService
{
    private readonly ILogger<PartitionInitializationService> _logger;
    private readonly IConfiguration _configuration;

    public PartitionInitializationService(
        ILogger<PartitionInitializationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔧 Partition Initialization Service starting...");

        try
        {
            await InitializePartitioningAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize partitioning");
            // Не бросаем исключение, чтобы приложение запустилось
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task InitializePartitioningAsync(CancellationToken ct)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Шаг 1: Проверяем существование Partition Function
        if (!await PartitionFunctionExistsAsync(connection, ct))
        {
            _logger.LogInformation("📝 Creating Partition Function...");
            await CreatePartitionFunctionAsync(connection, ct);
            _logger.LogInformation("✅ Partition Function created");
        }
        else
        {
            _logger.LogInformation("✅ Partition Function already exists");
        }

        // Шаг 2: Проверяем существование Partition Scheme
        if (!await PartitionSchemeExistsAsync(connection, ct))
        {
            _logger.LogInformation("📝 Creating Partition Scheme...");
            await CreatePartitionSchemeAsync(connection, ct);
            _logger.LogInformation("✅ Partition Scheme created");
        }
        else
        {
            _logger.LogInformation("✅ Partition Scheme already exists");
        }

        // Шаг 3: Выводим статистику
        await PrintPartitionStatsAsync(connection, ct);
    }

    private async Task<bool> PartitionFunctionExistsAsync(
        SqlConnection connection,
        CancellationToken ct)
    {
        var sql = @"
            SELECT COUNT(*) 
            FROM sys.partition_functions 
            WHERE name = 'PF_PositionsHistory_ByMonth'";

        await using var cmd = new SqlCommand(sql, connection);
        var count = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return count > 0;
    }

    private async Task<bool> PartitionSchemeExistsAsync(
        SqlConnection connection,
        CancellationToken ct)
    {
        var sql = @"
            SELECT COUNT(*) 
            FROM sys.partition_schemes 
            WHERE name = 'PS_PositionsHistory_ByMonth'";

        await using var cmd = new SqlCommand(sql, connection);
        var count = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return count > 0;
    }

    private async Task CreatePartitionFunctionAsync(
        SqlConnection connection,
        CancellationToken ct)
    {
        // Создаем базовые границы на текущий год + 1 год вперед
        var currentYear = DateTime.UtcNow.Year;
        var boundaries = new List<string>();

        // Границы для текущего года
        for (int month = 1; month <= 12; month++)
        {
            boundaries.Add($"'{currentYear:0000}-{month:00}-01'");
        }

        // Границы для следующего года
        for (int month = 1; month <= 12; month++)
        {
            boundaries.Add($"'{(currentYear + 1):0000}-{month:00}-01'");
        }

        var sql = $@"
            CREATE PARTITION FUNCTION [PF_PositionsHistory_ByMonth] (DATETIME2)
            AS RANGE RIGHT
            FOR VALUES ({string.Join(", ", boundaries)})";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = 120; // 2 минуты
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task CreatePartitionSchemeAsync(
        SqlConnection connection,
        CancellationToken ct)
    {
        var sql = @"
            CREATE PARTITION SCHEME [PS_PositionsHistory_ByMonth]
            AS PARTITION [PF_PositionsHistory_ByMonth]
            ALL TO ([PRIMARY])";

        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task PrintPartitionStatsAsync(
        SqlConnection connection,
        CancellationToken ct)
    {
        var sql = @"
            SELECT 
                pf.name AS PartitionFunction,
                ps.name AS PartitionScheme,
                COUNT(prv.boundary_id) AS BoundaryCount
            FROM sys.partition_functions pf
            LEFT JOIN sys.partition_schemes ps ON ps.function_id = pf.function_id
            LEFT JOIN sys.partition_range_values prv ON prv.function_id = pf.function_id
            WHERE pf.name = 'PF_PositionsHistory_ByMonth'
            GROUP BY pf.name, ps.name";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            var function = reader.GetString(0);
            var scheme = reader.IsDBNull(1) ? "N/A" : reader.GetString(1);
            var boundaries = reader.GetInt32(2);

            _logger.LogInformation(
                "📊 Partition Stats: Function={Function}, Scheme={Scheme}, Boundaries={Boundaries}",
                function, scheme, boundaries);
        }
    }
}