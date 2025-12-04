using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BinomoBackend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitionsHistoryPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================
            // ШАГ 1: Создаём Partition Function и Scheme
            // ============================================
            
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.partition_functions WHERE name = 'PF_PositionsHistory_ByMonth')
                BEGIN
                    CREATE PARTITION FUNCTION PF_PositionsHistory_ByMonth (DATETIME2)
                    AS RANGE RIGHT FOR VALUES ('2025-01-01');
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.partition_schemes WHERE name = 'PS_PositionsHistory_ByMonth')
                BEGIN
                    CREATE PARTITION SCHEME PS_PositionsHistory_ByMonth
                    AS PARTITION PF_PositionsHistory_ByMonth
                    ALL TO ([PRIMARY]);
                END
            ");

            // Добавляем границы на 12 месяцев вперёд
            for (int i = 1; i <= 12; i++)
            {
                var date = DateTime.UtcNow.AddMonths(i);
                var boundary = new DateTime(date.Year, date.Month, 1);
                
                migrationBuilder.Sql($@"
                    DECLARE @BoundaryExists INT;
                    SELECT @BoundaryExists = COUNT(*)
                    FROM sys.partition_range_values prv
                    INNER JOIN sys.partition_functions pf ON prv.function_id = pf.function_id
                    WHERE pf.name = 'PF_PositionsHistory_ByMonth'
                        AND CAST(prv.value AS DATETIME2) = '{boundary:yyyy-MM-dd}';
                    
                    IF @BoundaryExists = 0
                    BEGIN
                        ALTER PARTITION FUNCTION PF_PositionsHistory_ByMonth()
                        SPLIT RANGE ('{boundary:yyyy-MM-dd}');
                    END
                ");
            }

            // ============================================
            // ШАГ 2: Подготавливаем данные (исправляем NULL значения)
            // ============================================
            
            // Делаем ClosedAt NOT NULL (ставим CreatedAt если NULL)
            migrationBuilder.Sql(@"
                UPDATE PositionsHistory 
                SET ClosedAt = ISNULL(ClosedAt, CreatedAt)
                WHERE ClosedAt IS NULL;
            ");

            // Исправляем NULL в Leverage
            migrationBuilder.Sql(@"
                UPDATE PositionsHistory 
                SET Leverage = 1
                WHERE Leverage IS NULL;
            ");

            // ============================================
            // ШАГ 3: Изменяем существующую таблицу
            // ============================================

            // Удаляем старый PK
            migrationBuilder.DropPrimaryKey(
                name: "PK_PositionsHistory",
                table: "PositionsHistory");

            // Удаляем старые индексы
            migrationBuilder.DropIndex(
                name: "IX_PositionsHistory_ClosedAt",
                table: "PositionsHistory");

            migrationBuilder.DropIndex(
                name: "IX_PositionsHistory_Status",
                table: "PositionsHistory");

            migrationBuilder.DropIndex(
                name: "IX_PositionsHistory_Symbol",
                table: "PositionsHistory");

            // Делаем ClosedAt NOT NULL
            migrationBuilder.AlterColumn<DateTime>(
                name: "ClosedAt",
                table: "PositionsHistory",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            // ============================================
            // ШАГ 4: Конвертируем в партиционированную таблицу
            // ============================================

            migrationBuilder.Sql(@"
                -- Временно отключаем Foreign Key
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PositionsHistory_Users_UserId')
                    ALTER TABLE PositionsHistory DROP CONSTRAINT FK_PositionsHistory_Users_UserId;

                -- Переносим данные во временную таблицу
                SELECT * INTO #TempPositionsHistory FROM PositionsHistory;

                -- Удаляем старую таблицу
                DROP TABLE PositionsHistory;

                -- Создаём партиционированную таблицу
                CREATE TABLE PositionsHistory (
                    Id UNIQUEIDENTIFIER NOT NULL,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    Symbol NVARCHAR(20) NOT NULL,
                    Type NVARCHAR(10) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    OrderType NVARCHAR(10) NOT NULL,
                    EntryPrice DECIMAL(18,8) NOT NULL,
                    ExitPrice DECIMAL(18,8) NULL,
                    Amount DECIMAL(18,8) NOT NULL,
                    Margin DECIMAL(18,8) NOT NULL,
                    ProfitLoss DECIMAL(18,8) NULL,
                    ROI DECIMAL(18,8) NULL,
                    LimitPrice DECIMAL(18,8) NULL,
                    StopLoss DECIMAL(18,8) NULL,
                    TakeProfit DECIMAL(18,8) NULL,
                    Leverage INT NOT NULL DEFAULT 1,
                    CloseReason NVARCHAR(50) NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    ClosedAt DATETIME2 NOT NULL,
                    CONSTRAINT PK_PositionsHistory PRIMARY KEY (Id, ClosedAt)
                ) ON PS_PositionsHistory_ByMonth(ClosedAt);

                -- Восстанавливаем данные
                INSERT INTO PositionsHistory (
                    Id, UserId, Symbol, Type, Status, OrderType,
                    EntryPrice, ExitPrice, Amount, Margin,
                    ProfitLoss, ROI, LimitPrice, StopLoss, TakeProfit,
                    Leverage, CloseReason, CreatedAt, ClosedAt
                )
                SELECT 
                    Id, UserId, Symbol, Type, Status, OrderType,
                    EntryPrice, ExitPrice, Amount, Margin,
                    ProfitLoss, ROI, LimitPrice, StopLoss, TakeProfit,
                    ISNULL(Leverage, 1), CloseReason, CreatedAt, ClosedAt
                FROM #TempPositionsHistory;

                DROP TABLE #TempPositionsHistory;
            ");

            // ============================================
            // ШАГ 5: Создаём индексы и Foreign Key
            // ============================================

            migrationBuilder.CreateIndex(
                name: "IX_PositionsHistory_UserId",
                table: "PositionsHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionsHistory_UserId_ClosedAt",
                table: "PositionsHistory",
                columns: new[] { "UserId", "ClosedAt" },
                descending: new[] { false, true });

            migrationBuilder.Sql(@"
                ALTER TABLE PositionsHistory
                ADD CONSTRAINT FK_PositionsHistory_Users_UserId
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат изменений
            migrationBuilder.Sql(@"
                -- Сохраняем данные
                SELECT * INTO #TempPositionsHistory FROM PositionsHistory;

                -- Удаляем партиционированную таблицу
                IF OBJECT_ID('PositionsHistory', 'U') IS NOT NULL
                    DROP TABLE PositionsHistory;

                -- Создаём обычную таблицу
                CREATE TABLE PositionsHistory (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    Symbol NVARCHAR(20) NOT NULL,
                    Type NVARCHAR(10) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    OrderType NVARCHAR(10) NOT NULL,
                    EntryPrice DECIMAL(18,8) NOT NULL,
                    ExitPrice DECIMAL(18,8) NULL,
                    Amount DECIMAL(18,8) NOT NULL,
                    Margin DECIMAL(18,8) NOT NULL,
                    ProfitLoss DECIMAL(18,8) NULL,
                    ROI DECIMAL(18,8) NULL,
                    LimitPrice DECIMAL(18,8) NULL,
                    StopLoss DECIMAL(18,8) NULL,
                    TakeProfit DECIMAL(18,8) NULL,
                    Leverage INT NOT NULL DEFAULT 1,
                    CloseReason NVARCHAR(50) NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    ClosedAt DATETIME2 NULL
                );

                -- Восстанавливаем данные
                INSERT INTO PositionsHistory 
                SELECT * FROM #TempPositionsHistory;

                DROP TABLE #TempPositionsHistory;

                -- Восстанавливаем индексы
                CREATE INDEX IX_PositionsHistory_ClosedAt ON PositionsHistory(ClosedAt);
                CREATE INDEX IX_PositionsHistory_Status ON PositionsHistory(Status);
                CREATE INDEX IX_PositionsHistory_Symbol ON PositionsHistory(Symbol);

                -- Foreign Key
                ALTER TABLE PositionsHistory
                ADD CONSTRAINT FK_PositionsHistory_Users_UserId
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;
            ");

            // Удаляем Partition Scheme и Function
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.partition_schemes WHERE name = 'PS_PositionsHistory_ByMonth')
                    DROP PARTITION SCHEME PS_PositionsHistory_ByMonth;
                
                IF EXISTS (SELECT * FROM sys.partition_functions WHERE name = 'PF_PositionsHistory_ByMonth')
                    DROP PARTITION FUNCTION PF_PositionsHistory_ByMonth;
            ");
        }
    }
}