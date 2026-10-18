using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    OpenTimeUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    CloseTimeUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Open = table.Column<string>(type: "TEXT", nullable: false),
                    High = table.Column<string>(type: "TEXT", nullable: false),
                    Low = table.Column<string>(type: "TEXT", nullable: false),
                    Close = table.Column<string>(type: "TEXT", nullable: false),
                    Volume = table.Column<string>(type: "TEXT", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReceivedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StrategyType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastEvaluatedCandleOpenTimeUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyInstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfigVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    CandleOpenTimeUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CandleClose = table.Column<string>(type: "TEXT", nullable: false),
                    FastEma = table.Column<string>(type: "TEXT", nullable: false),
                    SlowEma = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    IsBackfill = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "IX_Candles_Exchange_Symbol_Interval_OpenTimeUtc",
                table: "Candles",
                columns: new[] { "Exchange", "Symbol", "Interval", "OpenTimeUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Signals_StrategyInstanceId_CandleOpenTimeUtc",
                table: "Signals",
                columns: new[] { "StrategyInstanceId", "CandleOpenTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Signals_StrategyInstanceId_ConfigVersion_CandleOpenTimeUtc_Action",
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
                name: "Candles");

            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.DropTable(
                name: "StrategyInstances");
        }
    }
}
