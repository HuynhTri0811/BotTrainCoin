using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandleTradeStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuoteVolume",
                table: "Candles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TradeCount",
                table: "Candles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuoteVolume",
                table: "Candles");

            migrationBuilder.DropColumn(
                name: "TradeCount",
                table: "Candles");
        }
    }
}
