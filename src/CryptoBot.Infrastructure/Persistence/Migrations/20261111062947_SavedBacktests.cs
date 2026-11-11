using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SavedBacktests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedBacktests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    StrategyInstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    FastPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    SlowPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    LookbackCandles = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstCandleUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    LastCandleUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    InitialCapital = table.Column<string>(type: "TEXT", nullable: false),
                    Sizing = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SizeValue = table.Column<string>(type: "TEXT", nullable: false),
                    TakeProfitPercent = table.Column<string>(type: "TEXT", nullable: true),
                    StopLossPercent = table.Column<string>(type: "TEXT", nullable: true),
                    FeePercent = table.Column<string>(type: "TEXT", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinRatePercent = table.Column<string>(type: "TEXT", nullable: false),
                    NetProfit = table.Column<string>(type: "TEXT", nullable: false),
                    NetProfitPercent = table.Column<string>(type: "TEXT", nullable: false),
                    MaxDrawdownPercent = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedBacktests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedBacktests_CreatedAtUtc",
                table: "SavedBacktests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SavedBacktests_StrategyInstanceId_CreatedAtUtc",
                table: "SavedBacktests",
                columns: new[] { "StrategyInstanceId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedBacktests");
        }
    }
}
