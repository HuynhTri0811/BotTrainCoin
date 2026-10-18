using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Application.MarketData;
using CryptoBot.Application.Signals;
using CryptoBot.Infrastructure.Exchanges.Binance;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CryptoBot.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Bot") ?? "Data Source=data/cryptobot.db";
        services.AddDbContext<BotDbContext>(o => o.UseSqlite(connectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICandleStore, CandleStore>();
        services.AddScoped<IStrategyInstanceStore, StrategyInstanceStore>();
        services.AddScoped<ISignalStore, SignalStore>();

        services.Configure<BinanceOptions>(config.GetSection("Binance"));
        services.AddHttpClient<IMarketDataClient, BinanceMarketDataClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            http.BaseAddress = new Uri(o.MarketDataBaseUrl);
            http.Timeout = o.RequestTimeout;
        });

        services.AddSingleton(config.GetSection("MarketData").Get<MarketDataOptions>() ?? new MarketDataOptions());
        services.AddSingleton(config.GetSection("Signals").Get<SignalOptions>() ?? new SignalOptions());
        services.AddSingleton<MarketDataStatusRegistry>();
        services.AddScoped<CandleSyncService>();
        services.AddScoped<SignalEvaluationService>();
        services.AddScoped<StrategyConfigurator>();
        services.AddScoped<ChartQueryService>();

        // Paper futures: mark price công khai từ Binance USDⓈ-M, khớp lệnh giả lập trong ứng dụng.
        services.AddSingleton(config.GetSection("PaperFutures").Get<PaperFuturesOptions>() ?? new PaperFuturesOptions());
        services.AddSingleton<MarkPriceCache>();
        services.AddSingleton<PaperFuturesLock>();
        services.AddScoped<IPaperFuturesStore, PaperFuturesStore>();
        services.AddScoped<PaperFuturesService>();
        services.AddHttpClient<IMarkPriceClient, BinanceFuturesMarkPriceClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            http.BaseAddress = new Uri(o.FuturesMarketDataBaseUrl);
            http.Timeout = o.RequestTimeout;
        });

        return services;
    }

    /// <summary>Áp migration và bật WAL. Chỉ một tiến trình được gọi hàm này.</summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(
            db.Database.GetConnectionString()).DataSource;
        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
