using System.Globalization;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
{
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<StrategyInstance> StrategyInstances => Set<StrategyInstance>();
    public DbSet<TradingSignal> Signals => Set<TradingSignal>();
    public DbSet<FuturesPosition> FuturesPositions => Set<FuturesPosition>();
    public DbSet<PaperLedgerEntry> PaperLedger => Set<PaperLedgerEntry>();
    public DbSet<AutoOrderRule> AutoOrderRules => Set<AutoOrderRule>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Tiền/giá/lượng: chuỗi decimal invariant chính xác. Không SUM/ORDER BY các cột này trong SQL.
        builder.Properties<decimal>().HaveConversion<DecimalInvariantStringConverter>().HaveColumnType("TEXT");
        // Thời gian: Unix milliseconds UTC, so sánh/sắp xếp được trong SQLite.
        builder.Properties<DateTime>().HaveConversion<UtcUnixMillisecondsConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Candle>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Exchange).HasMaxLength(32);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Interval).HasMaxLength(8);
            e.HasIndex(x => new { x.Exchange, x.Symbol, x.Interval, x.OpenTimeUtc }).IsUnique();
        });

        b.Entity<StrategyInstance>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.StrategyType).HasMaxLength(50);
            e.Property(x => x.Exchange).HasMaxLength(32);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Interval).HasMaxLength(8);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<TradingSignal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Interval).HasMaxLength(8);
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne<StrategyInstance>().WithMany().HasForeignKey(x => x.StrategyInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.StrategyInstanceId, x.ConfigVersion, x.CandleOpenTimeUtc, x.Action }).IsUnique();
            e.HasIndex(x => new { x.StrategyInstanceId, x.CandleOpenTimeUtc });
        });

        b.Entity<FuturesPosition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Environment).HasMaxLength(16);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Side).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.CloseReason).HasConversion<string>().HasMaxLength(16);
            e.Ignore(x => x.NetPnl);
            // Chống mở trùng khi cùng một lệnh được gửi lại.
            e.HasIndex(x => new { x.Environment, x.OpenCommandId }).IsUnique();
            e.HasIndex(x => new { x.Environment, x.Status, x.Symbol });
        });

        b.Entity<AutoOrderRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Environment).HasMaxLength(16);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Side).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.TriggerType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Condition).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.IndicatorName).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.TriggerDetail).HasMaxLength(500);
            e.Property(x => x.CloseDetail).HasMaxLength(500);
            e.Ignore(x => x.IsWatchingClose);
            e.HasIndex(x => x.CommandId).IsUnique();
            e.HasIndex(x => new { x.Environment, x.Status });
        });

        b.Entity<PaperLedgerEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Environment).HasMaxLength(16);
            e.Property(x => x.Asset).HasMaxLength(16);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Environment, x.TimeUtc });
        });
    }
}

public sealed class DecimalInvariantStringConverter() : ValueConverter<decimal, string>(
    v => v.ToString(CultureInfo.InvariantCulture),
    v => decimal.Parse(v, NumberStyles.Number, CultureInfo.InvariantCulture));

public sealed class UtcUnixMillisecondsConverter() : ValueConverter<DateTime, long>(
    v => ToUnixMs(v),
    v => DateTime.UnixEpoch.AddMilliseconds(v))
{
    private static long ToUnixMs(DateTime v)
    {
        if (v.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Chỉ lưu DateTime UTC.");
        return (v.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
    }
}
