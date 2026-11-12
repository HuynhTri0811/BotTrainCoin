using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Backtesting;
using CryptoBot.Application.Futures;
using CryptoBot.Application.Indicators;
using CryptoBot.Application.LivePrices;
using CryptoBot.Application.MarketData;
using CryptoBot.Application.News;
using CryptoBot.Application.Reports;
using CryptoBot.Application.Signals;
using CryptoBot.Infrastructure.Exchanges.Binance;
using CryptoBot.Infrastructure.Exchanges.Bitfinex;
using CryptoBot.Infrastructure.News;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // PostgreSQL trong container cryptobot-postgres (docker-compose.yml ở gốc repo).
        var connectionString = config.GetConnectionString("Bot")
                               ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Bot (PostgreSQL).");
        services.AddDbContext<BotDbContext>(builder => builder.UseNpgsql(connectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICandleStore, CandleStore>();
        services.AddScoped<IStrategyInstanceStore, StrategyInstanceStore>();
        services.AddScoped<ISignalStore, SignalStore>();

        services.Configure<BinanceOptions>(config.GetSection("Binance"));
        services.AddHttpClient<IMarketDataClient, BinanceMarketDataClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            http.BaseAddress = new Uri(options.MarketDataBaseUrl);
            http.Timeout = options.RequestTimeout;
        });

        services.AddSingleton(config.GetSection("MarketData").Get<MarketDataOptions>() ?? new MarketDataOptions());
        services.AddSingleton(config.GetSection("Signals").Get<SignalOptions>() ?? new SignalOptions());
        services.AddSingleton<MarketDataStatusRegistry>();
        services.AddScoped<CandleSyncService>();
        // Tải nến cũ cho backtest (chỉ đi lùi về trước nến sớm nhất đã lưu).
        services.AddScoped<CandleBackfillService>();
        services.AddScoped<SignalEvaluationService>();
        services.AddScoped<StrategyConfigurator>();
        services.AddScoped<ChartQueryService>();
        // Backtest chiến lược trên nến đã lưu (chỉ đọc DB).
        services.AddScoped<BacktestService>();
        services.AddScoped<BacktestOptionsService>();
        // Lịch sử backtest: mọi lần chạy được lưu để mở lại và so sánh.
        services.AddScoped<IBacktestStore, BacktestStore>();
        services.AddScoped<BacktestHistoryService>();
        // Nến thật quanh một lệnh của lần chạy đã lưu (chart chi tiết lệnh).
        services.AddScoped<BacktestTradeChartService>();

        // Chỉ báo tự viết: class có [Indicator] trong CryptoBot.Domain + cấu hình "Indicators".
        services.AddSingleton(sp => new IndicatorRegistry(
            config.GetSection("Indicators").Get<List<IndicatorDefinition>>() ?? [],
            sp.GetRequiredService<ILogger<IndicatorRegistry>>()));
        services.AddScoped<IndicatorService>();

        // Paper futures: mark price công khai từ Binance USDⓈ-M, khớp lệnh giả lập trong ứng dụng.
        services.AddSingleton(config.GetSection("PaperFutures").Get<PaperFuturesOptions>() ?? new PaperFuturesOptions());
        services.AddSingleton<MarkPriceCache>();
        services.AddSingleton<PaperFuturesLock>();
        services.AddScoped<IPaperFuturesStore, PaperFuturesStore>();
        services.AddScoped<PaperFuturesService>();
        services.AddSingleton(config.GetSection("AutoOrders").Get<AutoOrderOptions>() ?? new AutoOrderOptions());
        services.AddScoped<IAutoOrderStore, AutoOrderStore>();
        services.AddScoped<AutoOrderService>();
        services.AddHttpClient<IMarkPriceClient, BinanceFuturesMarkPriceClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            http.BaseAddress = new Uri(options.FuturesMarketDataBaseUrl);
            http.Timeout = options.RequestTimeout;
        });

        // Bảng giá trực tiếp: lấy Spot + Mark mỗi PollInterval để quan sát nguồn dữ liệu.
        services.AddSingleton(config.GetSection("LivePrices").Get<LivePriceOptions>() ?? new LivePriceOptions());
        services.AddSingleton<LivePriceHistory>();
        services.AddScoped<LivePriceSampler>();
        services.AddSingleton<DailyRangeService>();
        services.AddSingleton<SimilarDaysCache>();
        services.AddScoped<SimilarDaysService>();

        // Nến ngày lịch sử dài hạn (từ 2016) từ Bitfinex, lưu vào Candles với Exchange = "Bitfinex".
        services.Configure<BitfinexOptions>(config.GetSection("Bitfinex"));
        services.AddHttpClient<IDailyHistoryClient, BitfinexDailyHistoryClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<BitfinexOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = options.RequestTimeout;
            // Bitfinex trả 403 cho request không có User-Agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoBot/1.0");
        });
        services.AddSingleton(config.GetSection("DailyHistory").Get<DailyHistoryOptions>() ?? new DailyHistoryOptions());
        services.AddScoped<DailyHistorySyncService>();
        // Báo cáo biến động hằng ngày (06:00 giờ Việt Nam) từ nến Binance đã đóng.
        services.AddSingleton(config.GetSection("DailyReports").Get<DailyReportOptions>() ?? new DailyReportOptions());
        services.AddScoped<IDailyReportStore, DailyReportStore>();
        services.AddScoped<DailyReportService>();
        // Tin tức từ RSS công khai (không cần API key), đồng bộ mỗi NewsOptions.PollInterval.
        var news = config.GetSection("News").Get<NewsOptions>() ?? new NewsOptions();
        services.AddSingleton(news);
        services.AddHttpClient<INewsFeedClient, RssNewsFeedClient>(http =>
        {
            http.Timeout = news.RequestTimeout;
            // Một số trang chặn request không có User-Agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoBot/1.0");
        });
        services.AddScoped<INewsStore, NewsStore>();
        services.AddScoped<NewsSyncService>();
        // Sự kiện thị trường ghi tay (marketevents.json) để đối chiếu với các tháng tăng/giảm mạnh.
        services.AddSingleton(new MarketEventCatalog(config.GetSection("MarketEvents").Get<List<MarketEvent>>() ?? []));

        return services;
    }

    /// <summary>Áp migration. Chỉ một tiến trình được gọi hàm này.</summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
