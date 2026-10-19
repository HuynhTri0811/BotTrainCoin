using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndicatorAutoOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TriggerPrice",
                table: "AutoOrderRules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Condition",
                table: "AutoOrderRules",
                type: "TEXT",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<string>(
                name: "IndicatorName",
                table: "AutoOrderRules",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerDetail",
                table: "AutoOrderRules",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "AutoOrderRules",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                // Quy tắc đã có trước migration này đều là quy tắc theo giá.
                defaultValue: "Price");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndicatorName",
                table: "AutoOrderRules");

            migrationBuilder.DropColumn(
                name: "TriggerDetail",
                table: "AutoOrderRules");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "AutoOrderRules");

            migrationBuilder.AlterColumn<string>(
                name: "TriggerPrice",
                table: "AutoOrderRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Condition",
                table: "AutoOrderRules",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}
