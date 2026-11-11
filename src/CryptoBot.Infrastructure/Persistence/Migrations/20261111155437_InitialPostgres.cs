using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoOrderRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Condition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    TriggerPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    IndicatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Margin = table.Column<decimal>(type: "numeric", nullable: false),
                    Leverage = table.Column<int>(type: "integer", nullable: false),
                    StopLossPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    TakeProfitPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriggeredMarkPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    PositionId = table.Column<long>(type: "bigint", nullable: true),
                    TriggerDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CloseOnReverse = table.Column<bool>(type: "boolean", nullable: false),
                    CloseCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseWatchEndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoOrderRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Exchange = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    OpenTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CloseTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Open = table.Column<decimal>(type: "numeric", nullable: false),
                    High = table.Column<decimal>(type: "numeric", nullable: false),
                    Low = table.Column<decimal>(type: "numeric", nullable: false),
                    Close = table.Column<decimal>(type: "numeric", nullable: false),
                    Volume = table.Column<decimal>(type: "numeric", nullable: false),
                    QuoteVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    TradeCount = table.Column<long>(type: "bigint", nullable: true),
                    TakerBuyVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FuturesPositions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OpenCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloseCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Leverage = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Margin = table.Column<decimal>(type: "numeric", nullable: false),
                    LiquidationPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric", nullable: false),
                    TakeProfit = table.Column<decimal>(type: "numeric", nullable: true),
                    OpenFee = table.Column<decimal>(type: "numeric", nullable: false),
                    CloseFee = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CloseReason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ExitPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    RealizedPnl = table.Column<decimal>(type: "numeric", nullable: true),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuturesPositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Coins = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedBacktests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StrategyInstanceId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FastPeriod = table.Column<int>(type: "integer", nullable: false),
                    SlowPeriod = table.Column<int>(type: "integer", nullable: false),
                    LookbackCandles = table.Column<int>(type: "integer", nullable: false),
                    FirstCandleUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCandleUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitialCapital = table.Column<decimal>(type: "numeric", nullable: false),
                    Sizing = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SizeValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TakeProfitPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    StopLossPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    FeePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTrades = table.Column<int>(type: "integer", nullable: false),
                    WinRatePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    NetProfitPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDrawdownPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedBacktests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StrategyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: false),
                    Exchange = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastEvaluatedCandleOpenTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperLedger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Asset = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PositionId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperLedger_FuturesPositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "FuturesPositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StrategyInstanceId = table.Column<int>(type: "integer", nullable: false),
                    ConfigVersion = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CandleOpenTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CandleClose = table.Column<decimal>(type: "numeric", nullable: false),
                    FastEma = table.Column<decimal>(type: "numeric", nullable: false),
                    SlowEma = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsBackfill = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Signals_StrategyInstances_StrategyInstanceId",
                        column: x => x.StrategyInstanceId,
                        principalTable: "StrategyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoOrderRules_CommandId",
                table: "AutoOrderRules",
                column: "CommandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutoOrderRules_Environment_Status",
                table: "AutoOrderRules",
                columns: new[] { "Environment", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Exchange_Symbol_Interval_OpenTimeUtc",
                table: "Candles",
                columns: new[] { "Exchange", "Symbol", "Interval", "OpenTimeUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyReports_Symbol_PeriodEndUtc",
                table: "DailyReports",
                columns: new[] { "Symbol", "PeriodEndUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuturesPositions_Environment_OpenCommandId",
                table: "FuturesPositions",
                columns: new[] { "Environment", "OpenCommandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuturesPositions_Environment_Status_Symbol",
                table: "FuturesPositions",
                columns: new[] { "Environment", "Status", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_PublishedAtUtc",
                table: "NewsArticles",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_Source_ExternalId",
                table: "NewsArticles",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperLedger_Environment_TimeUtc",
                table: "PaperLedger",
                columns: new[] { "Environment", "TimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperLedger_PositionId",
                table: "PaperLedger",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedBacktests_CreatedAtUtc",
                table: "SavedBacktests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SavedBacktests_StrategyInstanceId_CreatedAtUtc",
                table: "SavedBacktests",
                columns: new[] { "StrategyInstanceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Signals_StrategyInstanceId_CandleOpenTimeUtc",
                table: "Signals",
                columns: new[] { "StrategyInstanceId", "CandleOpenTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Signals_StrategyInstanceId_ConfigVersion_CandleOpenTimeUtc_~",
                table: "Signals",
                columns: new[] { "StrategyInstanceId", "ConfigVersion", "CandleOpenTimeUtc", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategyInstances_Name",
                table: "StrategyInstances",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoOrderRules");

            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "DailyReports");

            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "PaperLedger");

            migrationBuilder.DropTable(
                name: "SavedBacktests");

            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.DropTable(
                name: "FuturesPositions");

            migrationBuilder.DropTable(
                name: "StrategyInstances");
        }
    }
}
