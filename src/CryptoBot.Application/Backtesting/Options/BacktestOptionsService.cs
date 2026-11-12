using System.Reflection;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Application.Indicators;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Indicators;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Gợi ý cho form backtest: chỉ báo tự viết nào chạy được và với tham số nào, phí/trượt giá/ký quỹ duy trì của Paper
/// Futures, margin/đòn bẩy/SL/TP mặc định của quy tắc tự động. Nhờ vậy backtest Futures mặc định giống lệnh thật.
/// </summary>
public sealed class BacktestOptionsService(
    IStrategyInstanceStore instances,
    IndicatorRegistry registry,
    PaperFuturesOptions paper,
    AutoOrderOptions autoOrders)
{
    /// <summary>Ký quỹ duy trì dùng khi cặp chưa có trong cấu hình Paper Futures (mức BTCUSDT).</summary>
    public const decimal FallbackMaintenanceMarginRate = 0.004m;

    /// <summary>Gợi ý cho instance; null nếu không có instance.</summary>
    public async Task<BacktestOptions?> GetAsync(int strategyId, CancellationToken ct)
    {
        if (await instances.GetAsync(strategyId, ct) is not { } instance)
            return null;

        var key = CandleKey.For(instance);
        var indicators = IndicatorRegistry.AvailableTypes
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                // Ưu tiên chỉ báo đang chạy trên đúng cặp/khung nến này: backtest đúng thứ quy tắc tự động đang dùng.
                var preset = registry.All.FirstOrDefault(configured =>
                    string.Equals(configured.Type, entry.Key, StringComparison.OrdinalIgnoreCase)
                    && configured.Key == key);
                var indicator = preset?.Indicator
                                ?? IndicatorRegistry.Create(entry.Key, new Dictionary<string, string?>());
                var description = entry.Value.GetCustomAttribute<IndicatorAttribute>()?.Description ?? "";
                return new BacktestIndicatorChoice(entry.Key, description, IndicatorRegistry.ReadParameters(indicator),
                    preset?.Name);
            })
            .ToList();

        var rate = paper.FindRules(instance.Symbol)?.MaintenanceMarginRate ?? FallbackMaintenanceMarginRate;
        var futures = new BacktestFuturesDefaults(paper.TakerFeeRate * 100, paper.SlippageRate * 100, rate * 100,
            FuturesBacktestSettings.MaxLeverage, autoOrders.DefaultMargin, autoOrders.DefaultLeverage,
            autoOrders.DefaultStopLossPercent, autoOrders.DefaultTakeProfitPercent);
        return new BacktestOptions(indicators, futures);
    }
}
