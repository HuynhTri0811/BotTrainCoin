using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutoOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoOrderRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CommandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TriggerPrice = table.Column<string>(type: "TEXT", nullable: false),
                    Margin = table.Column<string>(type: "TEXT", nullable: false),
                    Leverage = table.Column<int>(type: "INTEGER", nullable: false),
                    StopLossPercent = table.Column<string>(type: "TEXT", nullable: false),
                    TakeProfitPercent = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    TriggeredAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    TriggeredMarkPrice = table.Column<string>(type: "TEXT", nullable: true),
                    PositionId = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoOrderRules", x => x.Id);
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoOrderRules");
        }
    }
}
