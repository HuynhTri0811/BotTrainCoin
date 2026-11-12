using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.News;
using CryptoBot.Domain.Strategies;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
{
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<StrategyInstance> StrategyInstances => Set<StrategyInstance>();
    public DbSet<TradingSignal> Signals => Set<TradingSignal>();
    public DbSet<FuturesPosition> FuturesPositions => Set<FuturesPosition>();
    public DbSet<PaperLedgerEntry> PaperLedger => Set<PaperLedgerEntry>();
    public DbSet<AutoOrderRule> AutoOrderRules => Set<AutoOrderRule>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<SavedBacktest> SavedBacktests => Set<SavedBacktest>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Tiền/giá/lượng: numeric không giới hạn độ chính xác, giữ đúng mọi chữ số của decimal.
        builder.Properties<decimal>().HaveColumnType("numeric");
        // Thời gian: timestamptz. Npgsql chỉ nhận DateTime UTC, đúng quy ước của app.
        builder.Properties<DateTime>().HaveColumnType("timestamp with time zone");
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Candle>(entity =>
        {
            entity.HasKey(candle => candle.Id);
            entity.Property(candle => candle.Exchange).HasMaxLength(32);
            entity.Property(candle => candle.Symbol).HasMaxLength(32);
            entity.Property(candle => candle.Interval).HasMaxLength(8);
            entity.HasIndex(candle => new { candle.Exchange, candle.Symbol, candle.Interval,
                candle.OpenTimeUtc }).IsUnique();
        });

        builder.Entity<StrategyInstance>(entity =>
        {
            entity.HasKey(instance => instance.Id);
            entity.Property(instance => instance.Name).HasMaxLength(100);
            entity.Property(instance => instance.StrategyType).HasMaxLength(50);
            entity.Property(instance => instance.Exchange).HasMaxLength(32);
            entity.Property(instance => instance.Symbol).HasMaxLength(32);
            entity.Property(instance => instance.Interval).HasMaxLength(8);
            entity.HasIndex(instance => instance.Name).IsUnique();
        });

        builder.Entity<TradingSignal>(entity =>
        {
            entity.HasKey(signal => signal.Id);
            entity.Property(signal => signal.Symbol).HasMaxLength(32);
            entity.Property(signal => signal.Interval).HasMaxLength(8);
            entity.Property(signal => signal.Action).HasConversion<string>().HasMaxLength(8);
            entity.Property(signal => signal.Reason).HasMaxLength(500);
            entity.HasOne<StrategyInstance>().WithMany().HasForeignKey(signal => signal.StrategyInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(signal => new { signal.StrategyInstanceId, signal.ConfigVersion, signal.CandleOpenTimeUtc,
                signal.Action }).IsUnique();
            entity.HasIndex(signal => new { signal.StrategyInstanceId, signal.CandleOpenTimeUtc });
        });

        builder.Entity<FuturesPosition>(entity =>
        {
            entity.HasKey(position => position.Id);
            entity.Property(position => position.Environment).HasMaxLength(16);
            entity.Property(position => position.Symbol).HasMaxLength(32);
            entity.Property(position => position.Side).HasConversion<string>().HasMaxLength(8);
            entity.Property(position => position.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(position => position.CloseReason).HasConversion<string>().HasMaxLength(16);
            entity.Ignore(position => position.NetPnl);
            // Chống mở trùng khi cùng một lệnh được gửi lại.
            entity.HasIndex(position => new { position.Environment, position.OpenCommandId }).IsUnique();
            entity.HasIndex(position => new { position.Environment, position.Status, position.Symbol });
        });

        builder.Entity<AutoOrderRule>(entity =>
        {
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.Environment).HasMaxLength(16);
            entity.Property(rule => rule.Symbol).HasMaxLength(32);
            entity.Property(rule => rule.Side).HasConversion<string>().HasMaxLength(8);
            entity.Property(rule => rule.TriggerType).HasConversion<string>().HasMaxLength(16);
            entity.Property(rule => rule.Condition).HasConversion<string>().HasMaxLength(16);
            entity.Property(rule => rule.IndicatorName).HasMaxLength(100);
            entity.Property(rule => rule.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(rule => rule.Note).HasMaxLength(500);
            entity.Property(rule => rule.TriggerDetail).HasMaxLength(500);
            entity.Property(rule => rule.CloseDetail).HasMaxLength(500);
            entity.Ignore(rule => rule.IsWatchingClose);
            entity.HasIndex(rule => rule.CommandId).IsUnique();
            entity.HasIndex(rule => new { rule.Environment, rule.Status });
        });

        builder.Entity<DailyReport>(entity =>
        {
            entity.HasKey(report => report.Id);
            entity.Property(report => report.Symbol).HasMaxLength(32);
            entity.Property(report => report.Interval).HasMaxLength(8);
            // Một báo cáo cho mỗi (symbol, giờ lập): chạy lại hoặc tạo bù không sinh bản trùng.
            entity.HasIndex(report => new { report.Symbol, report.PeriodEndUtc }).IsUnique();
        });

        builder.Entity<SavedBacktest>(entity =>
        {
            entity.HasKey(saved => saved.Id);
            entity.Property(saved => saved.Symbol).HasMaxLength(32);
            entity.Property(saved => saved.Interval).HasMaxLength(8);
            entity.Property(saved => saved.Sizing).HasConversion<string>().HasMaxLength(16);
            entity.Property(saved => saved.SignalType).HasMaxLength(SavedBacktest.MaxSignalTypeLength);
            entity.Property(saved => saved.SignalParameters).HasMaxLength(SavedBacktest.MaxSignalParametersLength);
            entity.Property(saved => saved.Direction).HasConversion<string>().HasMaxLength(16);
            entity.Property(saved => saved.Note).HasMaxLength(SavedBacktest.MaxNoteLength);
            entity.Ignore(saved => saved.Settings);
            // Lịch sử liệt kê mới → cũ, có thể lọc theo chiến lược.
            entity.HasIndex(saved => new { saved.StrategyInstanceId, saved.CreatedAtUtc });
            entity.HasIndex(saved => saved.CreatedAtUtc);
        });

        builder.Entity<NewsArticle>(entity =>
        {
            entity.HasKey(article => article.Id);
            entity.Property(article => article.Source).HasMaxLength(64);
            entity.Property(article => article.ExternalId).HasMaxLength(500);
            entity.Property(article => article.Title).HasMaxLength(500);
            entity.Property(article => article.Url).HasMaxLength(1000);
            entity.Property(article => article.Summary).HasMaxLength(2000);
            entity.Property(article => article.Coins).HasMaxLength(32);
            // Đồng bộ lại không lưu trùng một bài của cùng nguồn.
            entity.HasIndex(article => new { article.Source, article.ExternalId }).IsUnique();
            entity.HasIndex(article => article.PublishedAtUtc);
        });

        builder.Entity<PaperLedgerEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Environment).HasMaxLength(16);
            entity.Property(entry => entry.Asset).HasMaxLength(16);
            entity.Property(entry => entry.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(entry => entry.Note).HasMaxLength(200);
            entity.HasOne(entry => entry.Position).WithMany().HasForeignKey(entry => entry.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(entry => new { entry.Environment, entry.TimeUtc });
        });
    }
}
