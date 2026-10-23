using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutoCloseOnReverse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CloseCommandId",
                table: "AutoOrderRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseDetail",
                table: "AutoOrderRules",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CloseOnReverse",
                table: "AutoOrderRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "CloseWatchEndedAtUtc",
                table: "AutoOrderRules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseCommandId",
                table: "AutoOrderRules");

            migrationBuilder.DropColumn(
                name: "CloseDetail",
                table: "AutoOrderRules");

            migrationBuilder.DropColumn(
                name: "CloseOnReverse",
                table: "AutoOrderRules");

            migrationBuilder.DropColumn(
                name: "CloseWatchEndedAtUtc",
                table: "AutoOrderRules");
        }
    }
}
