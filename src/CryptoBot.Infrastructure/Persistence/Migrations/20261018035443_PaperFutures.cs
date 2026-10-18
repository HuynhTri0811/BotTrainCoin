using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaperFutures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FuturesPositions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OpenCommandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloseCommandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Leverage = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<string>(type: "TEXT", nullable: false),
                    EntryPrice = table.Column<string>(type: "TEXT", nullable: false),
                    Margin = table.Column<string>(type: "TEXT", nullable: false),
                    LiquidationPrice = table.Column<string>(type: "TEXT", nullable: false),
                    StopLoss = table.Column<string>(type: "TEXT", nullable: false),
                    TakeProfit = table.Column<string>(type: "TEXT", nullable: true),
                    OpenFee = table.Column<string>(type: "TEXT", nullable: false),
                    CloseFee = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CloseReason = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    ExitPrice = table.Column<string>(type: "TEXT", nullable: true),
                    RealizedPnl = table.Column<string>(type: "TEXT", nullable: true),
                    OpenedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuturesPositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperLedger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Asset = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PositionId = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TimeUtc = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "IX_PaperLedger_Environment_TimeUtc",
                table: "PaperLedger",
                columns: new[] { "Environment", "TimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperLedger_PositionId",
                table: "PaperLedger",
                column: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperLedger");

            migrationBuilder.DropTable(
                name: "FuturesPositions");
        }
    }
}
