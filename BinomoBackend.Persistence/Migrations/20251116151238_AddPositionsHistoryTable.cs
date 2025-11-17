using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BinomoBackend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionsHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ProfitLoss",
                table: "Positions",
                type: "decimal(18,8)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,8)");

            migrationBuilder.CreateTable(
                name: "PositionsHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Margin = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ProfitLoss = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    ROI = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    Leverage = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LimitPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    StopLoss = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionsHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionsHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PositionsHistory_ClosedAt",
                table: "PositionsHistory",
                column: "ClosedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PositionsHistory_Status",
                table: "PositionsHistory",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PositionsHistory_Symbol",
                table: "PositionsHistory",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_PositionsHistory_UserId",
                table: "PositionsHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionsHistory");

            migrationBuilder.AlterColumn<decimal>(
                name: "ProfitLoss",
                table: "Positions",
                type: "decimal(18,8)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,8)",
                oldNullable: true);
        }
    }
}
