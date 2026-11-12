using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BacktestFuturesAndSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SlowPeriod",
                table: "SavedBacktests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "FastPeriod",
                table: "SavedBacktests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "CloseOnReverse",
                table: "SavedBacktests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "SavedBacktests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Leverage",
                table: "SavedBacktests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintenanceMarginPercent",
                table: "SavedBacktests",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignalParameters",
                table: "SavedBacktests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignalType",
                table: "SavedBacktests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                // Mọi lần chạy đã lưu trước bản này đều là EMA cross.
                defaultValue: "EmaCross");

            migrationBuilder.AddColumn<decimal>(
                name: "SlippagePercent",
                table: "SavedBacktests",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lần chạy theo chỉ báo tự viết không có tham số EMA, không lưu được ở bản cũ.
            migrationBuilder.Sql(
                "DELETE FROM \"SavedBacktests\" WHERE \"FastPeriod\" IS NULL OR \"SlowPeriod\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "CloseOnReverse",
                table: "SavedBacktests");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "SavedBacktests");

            migrationBuilder.DropColumn(
                name: "Leverage",
                table: "SavedBacktests");

            migrationBuilder.DropColumn(
                name: "MaintenanceMarginPercent",
                table: "SavedBacktests");

            migrationBuilder.DropColumn(
                name: "SignalParameters",
                table: "SavedBacktests");

            migrationBuilder.DropColumn(
                name: "SignalType",
                table: "SavedBacktests");

            migrationBuilder.DropColumn(
                name: "SlippagePercent",
                table: "SavedBacktests");

            migrationBuilder.AlterColumn<int>(
                name: "SlowPeriod",
                table: "SavedBacktests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FastPeriod",
                table: "SavedBacktests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
