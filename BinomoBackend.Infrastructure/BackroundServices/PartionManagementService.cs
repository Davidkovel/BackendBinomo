// Infrastructure/BackgroundServices/PartitionManagementService.cs
using System.Data.SqlClient;
using Microsoft.Data.SqlClient; // ← MS SQL
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Infrastructure.BackgroundServices;

/// <summary>
/// Automatic creating partions for MS SQL Server
/// </summary>
public class PartitionManagementService : BackgroundService
{
    private readonly ILogger<PartitionManagementService> _logger;
    private readonly IConfiguration _configuration;
    private const int MonthsAhead = 3; // Creating partions for 3 months ahead
    private const string PartitionFunctionName = "PF_PositionsHistory_ByMonth";
    private const string PartitionSchemeName = "PS_PositionsHistory_ByMonth";

    public PartitionManagementService(
        ILogger<PartitionManagementService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🗂️ Partition Management Service started");
        
        await EnsurePartitionsExist(stoppingToken);

        // Проверяем каждый день в 02:00
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            await EnsurePartitionsExist(stoppingToken);
        }
    }

    /// <summary>
    /// Checks and creates partions if need
    /// </summary>
    private async Task EnsurePartitionsExist(CancellationToken ct)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var existingBoundaries = await GetExistingPartitionBoundaries(connection, ct);

            _logger.LogDebug(
                "📊 Current partition boundaries: {Count}",
                existingBoundaries.Count);

            var boundariesToAdd = CalculateMissingBoundaries(existingBoundaries);

            // Add new partitions
            foreach (var boundary in boundariesToAdd)
            {
                await AddPartitionBoundary(connection, boundary, ct);
                _logger.LogInformation(
                    "✅ Added partition boundary: {Boundary:yyyy-MM-dd}",
                    boundary);
            }

            // 6. Archive old partitions
            await ArchiveOldPartitions(connection, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error managing partitions");
        }
    }

    /// <summary>
    /// Get list of exists partions
    /// </summary>
    private async Task<List<DateTime>> GetExistingPartitionBoundaries(
        SqlConnection connection,
        CancellationToken ct)
    {
        // SQL для получения границ партиций из системных таблиц
        var sql = @"
            SELECT CAST(prv.value AS DATETIME2) AS BoundaryValue
            FROM sys.partition_functions pf
            INNER JOIN sys.partition_range_values prv 
                ON pf.function_id = prv.function_id
            WHERE pf.name = @PartitionFunctionName
            ORDER BY prv.boundary_id";

        var boundaries = new List<DateTime>();

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@PartitionFunctionName", PartitionFunctionName);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            boundaries.Add(reader.GetDateTime(0));
        }

        return boundaries;
    }

    /// <summary>
    /// Calculating with partions should add
    /// </summary>
    private List<DateTime> CalculateMissingBoundaries(List<DateTime> existingBoundaries)
    {
        var boundaries = new List<DateTime>();
        var currentDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        for (int i = 0; i <= MonthsAhead; i++)
        {
            var targetDate = currentDate.AddMonths(i);

            // If partions is existing
            if (!existingBoundaries.Any(b => 
                b.Year == targetDate.Year && b.Month == targetDate.Month))
            {
                boundaries.Add(targetDate);
            }
        }

        return boundaries;
    }

    /// <summary>
    /// Add new partions
    /// 
    /// MS SQL Commands:
    /// ALTER PARTITION FUNCTION PF_Name()
    /// SPLIT RANGE ('2025-01-01')
    /// 
    /// </summary>
    private async Task AddPartitionBoundary(
        SqlConnection connection,
        DateTime boundary,
        CancellationToken ct)
    {
        var sql = $@"
            ALTER PARTITION FUNCTION [{PartitionFunctionName}]()
            SPLIT RANGE ('{boundary:yyyy-MM-dd}')";

        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Archive old partions
    /// 
    /// MS SQL Command:
    /// ALTER PARTITION FUNCTION PF_Name()
    /// MERGE RANGE ('2024-01-01')
    /// </summary>
    private async Task ArchiveOldPartitions(
        SqlConnection connection,
        CancellationToken ct)
    {
        // Deleting partions older 6 months
        var archiveDate = DateTime.UtcNow.AddMonths(-6);
        var archiveBoundary = new DateTime(archiveDate.Year, archiveDate.Month, 1);

        var sql = @"
            SELECT CAST(prv.value AS DATETIME2) AS BoundaryValue
            FROM sys.partition_functions pf
            INNER JOIN sys.partition_range_values prv 
                ON pf.function_id = prv.function_id
            WHERE pf.name = @PartitionFunctionName
                AND CAST(prv.value AS DATETIME2) < @ArchiveBoundary
            ORDER BY prv.boundary_id";

        var oldBoundaries = new List<DateTime>();

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@PartitionFunctionName", PartitionFunctionName);
        cmd.Parameters.AddWithValue("@ArchiveBoundary", archiveBoundary);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            oldBoundaries.Add(reader.GetDateTime(0));
        }

        await reader.CloseAsync();

        // MERGE старые границы
        foreach (var boundary in oldBoundaries)
        {
            _logger.LogInformation(
                "📦 Archiving partition boundary: {Boundary:yyyy-MM-dd}",
                boundary);

            // ОПЦИЯ 1: MERGE (объединяем партиции)
            await MergePartitionBoundary(connection, boundary, ct); // MERGE MODE |||| MERGE удаляет границу и объединяет две партиции в одну.

            // ОПЦИЯ 2: Экспорт в архив (раскомментируйте если нужно)
            // await ExportPartitionToArchive(connection, boundary, ct); // SWITCH MODE |||| @Todo: позже на s3 перевести для старых границ
        }
    }

    /// <summary>
    /// Join partions (deleting partition)
    /// </summary>
    private async Task MergePartitionBoundary(
        SqlConnection connection,
        DateTime boundary,
        CancellationToken ct)
    {
        var sql = $@"
            ALTER PARTITION FUNCTION [{PartitionFunctionName}]()
            MERGE RANGE ('{boundary:yyyy-MM-dd}')";

        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(ct);

        _logger.LogInformation(
            "✅ Merged partition boundary: {Boundary:yyyy-MM-dd}",
            boundary);
    }

    /// <summary>
    /// Альтернатива: переключение партиции на архивную таблицу
    /// 
    /// MS SQL КОМАНДА:
    /// ALTER TABLE PositionsHistory
    /// SWITCH PARTITION $PARTITION.PF_Name('2024-01-01')
    /// TO ArchiveTable
    /// 
    /// ПРЕИМУЩЕСТВА:
    /// - Мгновенная операция (только метаданные)
    /// - Данные физически остаются на месте
    /// - Можно переместить на дешевое хранилище
    /// </summary>
    private async Task ExportPartitionToArchive(
        SqlConnection connection,
        DateTime boundary,
        CancellationToken ct)
    {
        // Создаем архивную таблицу если не существует
        var createArchiveTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PositionsHistory_Archive')
            BEGIN
                CREATE TABLE [dbo].[PositionsHistory_Archive] (
                    -- Same structure as PositionsHistory
                    [Id] UNIQUEIDENTIFIER NOT NULL,
                    [UserId] UNIQUEIDENTIFIER NOT NULL,
                    [Symbol] NVARCHAR(20) NOT NULL,
                    [ClosedAt] DATETIME2 NULL,
                    -- ... other columns
                    CONSTRAINT [PK_PositionsHistory_Archive] PRIMARY KEY ([Id], [ClosedAt])
                )
            END";

        await using var createCmd = new SqlCommand(createArchiveTableSql, connection);
        await createCmd.ExecuteNonQueryAsync(ct);

        // Переключаем партицию в архив
        var partitionNumber = await GetPartitionNumber(connection, boundary, ct);

        var switchSql = $@"
            ALTER TABLE [dbo].[PositionsHistory]
            SWITCH PARTITION {partitionNumber}
            TO [dbo].[PositionsHistory_Archive]";

        await using var switchCmd = new SqlCommand(switchSql, connection);
        await switchCmd.ExecuteNonQueryAsync(ct);

        _logger.LogInformation(
            "✅ Switched partition {PartitionNumber} to archive",
            partitionNumber);
    }

    /// <summary>
    /// Get partition number
    /// </summary>
    private async Task<int> GetPartitionNumber(
        SqlConnection connection,
        DateTime boundary,
        CancellationToken ct)
    {
        var sql = $@"
            SELECT $PARTITION.{PartitionFunctionName}('{boundary:yyyy-MM-dd}')";

        await using var cmd = new SqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync(ct);

        return Convert.ToInt32(result);
    }
}