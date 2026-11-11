using CryptoBot.Domain.Common;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Chạy một chiến lược trên chuỗi nến lịch sử và giả lập lệnh Long (không đòn bẩy) để thống kê.
///
/// Tín hiệu giữ nguyên như bot: mỗi nến gọi đúng <see cref="IStrategy.Evaluate"/> với cửa sổ
/// <see cref="IStrategy.RequiredCandles"/> nến kết thúc tại nến đó, bỏ qua cửa sổ có nến thiếu, và trạng thái
/// "đang giữ" đi theo chuỗi tín hiệu (BUY gần nhất chưa có SELL) như <c>SignalEvaluationService</c>. Vì vậy
/// TP/SL không làm đổi chuỗi BUY/SELL; sau khi TP/SL đóng lệnh, lệnh mới chỉ mở ở BUY kế tiếp (sau một SELL).
///
/// Khớp lệnh (thận trọng, nguyên tắc A4):
/// <list type="bullet">
/// <item>Tín hiệu có lúc nến đóng → lệnh vào/ra khớp ở giá mở của nến kế tiếp, không dùng giá đóng của nến tín hiệu.</item>
/// <item>TP/SL xét trên High/Low của từng nến kể cả nến vừa vào lệnh. Cùng một nến chạm cả hai → tính SL.</item>
/// <item>Giá mở nhảy qua mức TP/SL → khớp ở giá mở (SL tệ hơn mức đặt, TP không đẹp hơn giá mở).</item>
/// <item>Phí <see cref="BacktestSettings.FeePercent"/> tính trên giá trị lệnh ở cả lần vào và lần ra.</item>
/// </list>
/// </summary>
public static class Backtester
{
    // Kiểm tra hủy theo lô nến: đủ thường để dừng nhanh, không tốn công ở mỗi nến.
    private const int CancellationCheckInterval = 1024;

    /// <summary>Chạy backtest.</summary>
    /// <param name="candles">Nến đã đóng, tăng dần theo thời gian. Nến đầu dùng làm warm-up cho cửa sổ.</param>
    /// <param name="interval">Khung nến, để phát hiện cửa sổ có nến thiếu.</param>
    /// <param name="strategy">Chiến lược ra quyết định BUY/SELL.</param>
    /// <param name="settings">Vốn, khối lượng, TP/SL, phí.</param>
    /// <param name="cancellationToken">Dừng giữa chừng khi request bị hủy (vòng lặp nặng CPU, có thể vài chục giây).</param>
    /// <exception cref="ArgumentException">Khi <paramref name="settings"/> không hợp lệ.</exception>
    /// <exception cref="OperationCanceledException">Khi <paramref name="cancellationToken"/> bị hủy.</exception>
    public static BacktestResult Run(IReadOnlyList<Candle> candles, CandleInterval interval, IStrategy strategy,
        BacktestSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.Validate() is { } error)
            throw new ArgumentException(error, nameof(settings));

        Simulation simulation = new(settings);
        List<BacktestSignal> signals = [];
        List<EquityPoint> equity = [];
        bool botHolding = false;
        bool pendingEntry = false;
        bool pendingExit = false;
        int evaluated = 0;
        int skippedGaps = 0;
        int firstIndex = strategy.RequiredCandles - 1;

        for (int index = 0; index < candles.Count; index++)
        {
            if (index % CancellationCheckInterval == 0)
                cancellationToken.ThrowIfCancellationRequested();
            Candle candle = candles[index];

            // 1. Lệnh chờ từ tín hiệu ở nến trước khớp ở giá mở nến này.
            if (pendingEntry)
                simulation.Enter(candle);
            if (pendingExit)
                simulation.Exit(candle.OpenTimeUtc, candle.Open, BacktestExitReason.Signal);
            pendingEntry = false;
            pendingExit = false;

            // 2. TP/SL trong nến (kể cả nến vừa vào lệnh).
            simulation.CheckStops(candle);

            if (index < firstIndex)
                continue;

            // 3. Hỏi chiến lược ở lúc nến đóng, đúng như SignalEvaluationService.
            ListSlice<Candle> window = new(candles, index - firstIndex, strategy.RequiredCandles);
            if (CandleWindow.HasGap(window, interval))
            {
                skippedGaps++;
            }
            else
            {
                evaluated++;
                StrategyDecision decision = strategy.Evaluate(
                    new StrategyContext(window, botHolding, HasPendingEntry: false, ConfigVersion: 1));
                if (decision.Action == SignalAction.Buy)
                {
                    botHolding = true;
                    pendingEntry = !simulation.HasPosition;
                    signals.Add(new BacktestSignal(candle.OpenTimeUtc, decision.Action, candle.Close, decision.Reason));
                }
                else if (decision.Action == SignalAction.Sell)
                {
                    botHolding = false;
                    // TP/SL có thể đã đóng lệnh trước SELL: tín hiệu vẫn được ghi, chỉ không có gì để bán.
                    pendingExit = simulation.HasPosition;
                    signals.Add(new BacktestSignal(candle.OpenTimeUtc, decision.Action, candle.Close, decision.Reason));
                }
            }

            equity.Add(new EquityPoint(candle.OpenTimeUtc, simulation.EquityAt(candle.Close)));
        }

        Candle? last = candles.Count > 0 ? candles[^1] : null;
        BacktestOpenTrade? openTrade = last is null ? null : simulation.OpenTradeAt(last.Close);
        decimal firstClose = candles.Count > firstIndex ? candles[firstIndex].Close : 0m;
        BacktestStatistics statistics = BacktestStatistics.From(settings.InitialCapital, simulation.Trades,
            openTrade, equity, firstClose, last?.Close ?? 0m);

        return new BacktestResult(settings, statistics, simulation.Trades, openTrade, signals, equity, evaluated,
            skippedGaps);
    }

    /// <summary>Tiền mặt, lệnh đang mở và danh sách lệnh đã đóng của một lần backtest.</summary>
    private sealed class Simulation(BacktestSettings settings)
    {
        private readonly decimal _feeRate = settings.FeePercent / 100;
        private decimal _cash = settings.InitialCapital;
        private OpenLot? _lot;

        public List<BacktestTrade> Trades { get; } = [];

        public bool HasPosition => _lot is not null;

        /// <summary>Vào lệnh ở giá mở của <paramref name="candle"/>. Số tiền không vượt quá tiền mặt sau phí.</summary>
        public void Enter(Candle candle)
        {
            if (_lot is not null || candle.Open <= 0)
                return;

            decimal wanted = settings.Sizing == PositionSizing.PercentOfEquity
                ? _cash * settings.SizeValue / 100
                : settings.SizeValue;
            decimal amount = Math.Min(wanted, _cash / (1 + _feeRate));
            if (amount <= 0)
                return;

            decimal fee = amount * _feeRate;
            _cash -= amount + fee;
            decimal price = candle.Open;
            _lot = new OpenLot(candle.OpenTimeUtc, price, amount / price, amount, fee,
                settings.TakeProfitPercent is { } takeProfit ? price * (1 + takeProfit / 100) : null,
                settings.StopLossPercent is { } stopLoss ? price * (1 - stopLoss / 100) : null);
        }

        /// <summary>Đóng lệnh nếu giá trong nến chạm SL hoặc TP. SL được xét trước (thận trọng).</summary>
        public void CheckStops(Candle candle)
        {
            if (_lot is null)
                return;

            if (_lot.StopLossPrice is { } stop && candle.Low <= stop)
                Exit(candle.OpenTimeUtc, Math.Min(candle.Open, stop), BacktestExitReason.StopLoss);
            else if (_lot.TakeProfitPrice is { } take && candle.High >= take)
                Exit(candle.OpenTimeUtc, Math.Max(candle.Open, take), BacktestExitReason.TakeProfit);
        }

        /// <summary>Bán toàn bộ ở <paramref name="price"/>, trừ phí, ghi lại lệnh.</summary>
        public void Exit(DateTime timeUtc, decimal price, BacktestExitReason reason)
        {
            if (_lot is null)
                return;

            decimal proceeds = _lot.Quantity * price;
            decimal fee = proceeds * _feeRate;
            _cash += proceeds - fee;
            decimal netPnl = proceeds - fee - _lot.Amount - _lot.EntryFee;
            Trades.Add(new BacktestTrade(_lot.EntryTimeUtc, _lot.EntryPrice, timeUtc, price, _lot.Quantity,
                _lot.EntryFee + fee, netPnl, netPnl / _lot.Amount * 100, reason));
            _lot = null;
        }

        /// <summary>Tiền mặt + coin đang giữ theo <paramref name="price"/>.</summary>
        public decimal EquityAt(decimal price) => _cash + (_lot is null ? 0m : _lot.Quantity * price);

        /// <summary>Lệnh còn mở, định giá theo <paramref name="price"/>; null nếu không giữ gì.</summary>
        public BacktestOpenTrade? OpenTradeAt(decimal price) => _lot is null
            ? null
            : new BacktestOpenTrade(_lot.EntryTimeUtc, _lot.EntryPrice, _lot.Quantity, _lot.EntryFee, price,
                _lot.Quantity * price - _lot.Amount - _lot.EntryFee);
    }

    /// <summary>Lệnh đang mở: giá vào, số coin, tiền đã bỏ ra và mức TP/SL tính từ giá vào thực tế.</summary>
    private sealed record OpenLot(
        DateTime EntryTimeUtc,
        decimal EntryPrice,
        decimal Quantity,
        decimal Amount,
        decimal EntryFee,
        decimal? TakeProfitPrice,
        decimal? StopLossPrice);
}
