using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CryptoBot.IntegrationTests;

/// <summary>
/// Database PostgreSQL tạm với migration thật, xóa khi xong. Cần container cryptobot-postgres đang chạy
/// (<c>docker compose up -d</c>); đổi server bằng biến môi trường <c>CRYPTOBOT_TEST_DB</c> (chuỗi kết nối không có
/// Database).
/// </summary>
internal sealed class PostgresTestDatabase
{
    private static readonly string ServerConnection = Environment.GetEnvironmentVariable("CRYPTOBOT_TEST_DB")
        ?? "Host=localhost;Port=5433;Username=cryptobot;Password=cryptobot";

    private readonly string _database = $"cryptobot_test_{Guid.NewGuid():N}";

    private string ConnectionString => new NpgsqlConnectionStringBuilder(ServerConnection)
    {
        Database = _database,
        // Không giữ kết nối trong pool, để xóa được database khi xong.
        Pooling = false,
    }.ConnectionString;

    /// <summary>Mỗi lần gọi là một DbContext riêng, như hai request/worker khác nhau.</summary>
    public BotDbContext NewContext() =>
        new(new DbContextOptionsBuilder<BotDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task CreateAsync()
    {
        await using BotDbContext db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DropAsync()
    {
        await using NpgsqlConnection connection = new(
            new NpgsqlConnectionStringBuilder(ServerConnection) { Database = "postgres", Pooling = false }
                .ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
