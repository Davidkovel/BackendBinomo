using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BinomoBackend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiquidationPriceToPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LiquidationPrice",
                table: "Positions",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LiquidationPrice",
                table: "Positions");
        }
    }
}
