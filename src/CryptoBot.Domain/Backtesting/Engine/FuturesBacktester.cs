using CryptoBot.Domain.Common;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Backtest Futures (USDⓈ-M, isolated): lệnh Long/Short có đòn bẩy, tính giống lệnh tự động Paper Futures.
///
/// Tín hiệu: mỗi nến hỏi <see cref="IBacktestSignalSource"/> với cửa sổ
/// <see cref="IBacktestSignalSource.RequiredCandles"/> nến kết thúc tại nến đó; cửa sổ có nến thiếu thì bỏ qua (như
/// <see cref="Backtester"/>). Tại mỗi tín hiệu:
/// <list type="bullet">
/// <item>
/// Đang đứng ngoài và hướng tín hiệu được phép (<see cref="FuturesBacktestSettings.Direction"/>) → mở lệnh.
/// </item>
/// <item>Đang giữ cùng chiều → giữ, không vào chồng.</item>
/// <item>
/// Đang giữ chiều ngược lại → đóng nếu bật <see cref="FuturesBacktestSettings.CloseOnReverse"/>, rồi mở chiều mới nếu
/// được phép (đảo chiều); tắt thì giữ tới TP/SL/thanh lý.
/// </item>
/// </list>
///
/// Khớp lệnh (thận trọng như <see cref="Backtester"/>):
/// <list type="bullet">
/// <item>
/// Tín hiệu lúc nến đóng → đóng/mở ở giá mở nến kế tiếp, cộng trượt giá bất lợi. Giá mở đã qua giá thanh lý thì lệnh
/// đang giữ bị thanh lý trước.
/// </item>
/// <item>
/// Margin = <see cref="BacktestSettings.SizeValue"/> (% tiền đang có hoặc USDT cố định), giá trị vị thế = margin × đòn
/// bẩy, phí tính trên giá trị vị thế ở cả lần mở và đóng; lỗ của một lệnh không vượt quá margin.
/// </item>
/// <item>
/// Trong nến, kể cả nến vừa mở lệnh: chạm SL → cắt lỗ (giá thanh lý luôn nằm sau SL); chạm giá thanh lý → mất
/// margin; chạm TP → chốt lời. Cùng nến chạm SL và TP → tính SL.
/// </item>
/// <item>Không tính phí funding và các bậc ký quỹ (giống Paper Futures).</item>
/// </list>
/// </summary>
public static class FuturesBacktester
{
    private const int CancellationCheckInterval = 1024;

    /// <summary>Chạy backtest.</summary>
    /// <param name="candles">Nến đã đóng, tăng dần theo thời gian. Nến đầu dùng làm warm-up cho cửa sổ.</param>
    /// <param name="interval">Khung nến, để phát hiện cửa sổ có nến thiếu.</param>
    /// <param name="signals">Nguồn tín hiệu Long/Short.</param>
    /// <param name="settings">Vốn, margin, TP/SL, phí và <see cref="BacktestSettings.Futures"/> (bắt buộc).</param>
    /// <param name="cancellationToken">Dừng giữa chừng khi request bị hủy.</param>
    /// <exception cref="ArgumentException">Khi <paramref name="settings"/> sai hoặc không có phần Futures.</exception>
    /// <exception cref="OperationCanceledException">Khi <paramref name="cancellationToken"/> bị hủy.</exception>
    public static BacktestResult Run(IReadOnlyList<Candle> candles, CandleInterval interval,
        IBacktestSignalSource signals, BacktestSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.Validate() is { } error)
            throw new ArgumentException(error, nameof(settings));
        if (settings.Futures is not { } futures)
            throw new ArgumentException("Backtest Futures cần thiết lập đòn bẩy và hướng lệnh.", nameof(settings));

        Simulation simulation = new(settings, futures);
        List<BacktestSignal> signalLog = [];
        List<EquityPoint> equity = [];
        bool pendingClose = false;
        PositionSide? pendingOpen = null;
        int evaluated = 0;
        int skippedGaps = 0;
        int firstIndex = signals.RequiredCandles - 1;

        for (int index = 0; index < candles.Count; index++)
        {
            if (index % CancellationCheckInterval == 0)
                cancellationToken.ThrowIfCancellationRequested();
            Candle candle = candles[index];

            // 1. Giá mở đã nhảy qua giá thanh lý thì lệnh bị thanh lý trước khi lệnh đóng theo tín hiệu kịp khớp.
            simulation.CheckGapLiquidation(candle);

            // 2. Lệnh chờ từ tín hiệu ở nến trước khớp ở giá mở nến này: đóng trước rồi mở (đảo chiều).
            if (pendingClose)
                simulation.Close(candle.OpenTimeUtc, candle.Open, BacktestExitReason.Signal);
            if (pendingOpen is { } side)
                simulation.Open(candle, side);
            pendingClose = false;
            pendingOpen = null;

            // 3. Thanh lý/SL/TP trong nến (kể cả nến vừa mở lệnh).
            simulation.CheckStops(candle);

            if (index < firstIndex)
                continue;

            // 4. Đọc tín hiệu lúc nến đóng.
            ListSlice<Candle> window = new(candles, index - firstIndex, signals.RequiredCandles);
            if (CandleWindow.HasGap(window, interval))
            {
                skippedGaps++;
            }
            else
            {
                evaluated++;
                BacktestSignalReading reading = signals.Read(window);
                if (reading.Side is { } signalSide)
                {
                    signalLog.Add(new BacktestSignal(candle.OpenTimeUtc,
                        signalSide == PositionSide.Long ? SignalAction.Buy : SignalAction.Sell, candle.Close,
                        reading.Reason));
                    (pendingClose, pendingOpen) = Decide(simulation.Side, signalSide, futures);
                }
            }

            equity.Add(new EquityPoint(candle.OpenTimeUtc, simulation.EquityAt(candle.Close)));
        }

        Candle? last = candles.Count > 0 ? candles[^1] : null;
        BacktestOpenTrade? openTrade = last is null ? null : simulation.OpenTradeAt(last.Close);
        decimal firstClose = candles.Count > firstIndex ? candles[firstIndex].Close : 0m;
        BacktestStatistics statistics = BacktestStatistics.From(settings.InitialCapital, simulation.Trades,
            openTrade, equity, firstClose, last?.Close ?? 0m);

        return new BacktestResult(settings, statistics, simulation.Trades, openTrade, signalLog, equity, evaluated,
            skippedGaps);
    }

    /// <summary>Đóng lệnh đang giữ hay không, và mở lệnh chiều nào ở nến kế tiếp.</summary>
    /// <param name="holding">Chiều đang giữ, null nếu đứng ngoài.</param>
    /// <param name="signal">Chiều của tín hiệu vừa có.</param>
    /// <param name="futures">Hướng được phép và có đóng khi đảo chiều không.</param>
    internal static (bool Close, PositionSide? Open) Decide(PositionSide? holding, PositionSide signal,
        FuturesBacktestSettings futures)
    {
        if (holding == signal)
            return (false, null);

        bool allowed = futures.Direction switch
        {
            BacktestDirection.LongOnly => signal == PositionSide.Long,
            BacktestDirection.ShortOnly => signal == PositionSide.Short,
            _ => true,
        };
        if (holding is null)
            return (false, allowed ? signal : null);
        // Đang giữ chiều ngược lại: chỉ đóng (và đảo chiều) khi bật đóng khi đảo chiều.
        return futures.CloseOnReverse ? (true, allowed ? signal : null) : (false, null);
    }

    /// <summary>Tiền trong ví, lệnh đang mở và danh sách lệnh đã đóng của một lần backtest Futures.</summary>
    private sealed class Simulation(BacktestSettings settings, FuturesBacktestSettings futures)
    {
        private readonly decimal _feeRate = settings.FeePercent / 100;
        private readonly decimal _slippageRate = futures.SlippagePercent / 100;
        private readonly decimal _maintenanceRate = futures.MaintenanceMarginPercent / 100;

        // Tiền chưa dùng làm margin (margin của lệnh đang mở đã bị trừ ra).
        private decimal _cash = settings.InitialCapital;
        private OpenPosition? _position;

        public List<BacktestTrade> Trades { get; } = [];

        public PositionSide? Side => _position?.Side;

        /// <summary>
        /// Mở lệnh ở giá mở của <paramref name="candle"/> cộng trượt giá. Margin + phí không vượt tiền đang có.
        /// </summary>
        public void Open(Candle candle, PositionSide side)
        {
            if (_position is not null || candle.Open <= 0)
                return;

            decimal wanted = settings.Sizing == PositionSizing.PercentOfEquity
                ? _cash * settings.SizeValue / 100
                : settings.SizeValue;
            decimal margin = Math.Min(wanted, _cash / (1 + _feeRate * futures.Leverage));
            if (margin <= 0)
                return;

            decimal price = FuturesMath.ApplySlippage(candle.Open, side, opening: true, _slippageRate);
            decimal notional = margin * futures.Leverage;
            decimal fee = notional * _feeRate;
            _cash -= margin + fee;
            decimal direction = side == PositionSide.Long ? 1m : -1m;
            _position = new OpenPosition(side, candle.OpenTimeUtc, price, notional / price, margin, fee,
                FuturesMath.LiquidationPrice(side, price, futures.Leverage, _maintenanceRate),
                settings.TakeProfitPercent is { } takeProfit ? price * (1 + direction * takeProfit / 100) : null,
                settings.StopLossPercent is { } stopLoss ? price * (1 - direction * stopLoss / 100) : null);
        }

        /// <summary>Thanh lý ở giá mở nếu giá mở của <paramref name="candle"/> đã qua giá thanh lý.</summary>
        public void CheckGapLiquidation(Candle candle)
        {
            if (_position is { } position
                && FuturesMath.IsLiquidated(position.Side, candle.Open, position.LiquidationPrice))
                Liquidate(candle.OpenTimeUtc, candle.Open);
        }

        /// <summary>Đóng lệnh nếu giá trong nến chạm giá thanh lý, SL hoặc TP (theo thứ tự thận trọng).</summary>
        public void CheckStops(Candle candle)
        {
            if (_position is not { } position)
                return;

            bool isLong = position.Side == PositionSide.Long;
            decimal worst = isLong ? candle.Low : candle.High;
            decimal best = isLong ? candle.High : candle.Low;

            // Giá mở đã qua giá thanh lý được xét trước ở CheckGapLiquidation.
            if (position.StopLossPrice is { } stop && FuturesMath.StopHit(position.Side, worst, stop))
                Close(candle.OpenTimeUtc, isLong ? Math.Min(candle.Open, stop) : Math.Max(candle.Open, stop),
                    BacktestExitReason.StopLoss);
            else if (FuturesMath.IsLiquidated(position.Side, worst, position.LiquidationPrice))
                Liquidate(candle.OpenTimeUtc, position.LiquidationPrice);
            else if (position.TakeProfitPrice is { } take && FuturesMath.TakeProfitHit(position.Side, best, take))
                Close(candle.OpenTimeUtc, isLong ? Math.Max(candle.Open, take) : Math.Min(candle.Open, take),
                    BacktestExitReason.TakeProfit);
        }

        /// <summary>Đóng toàn bộ ở <paramref name="price"/> trừ trượt giá, trừ phí, ghi lại lệnh.</summary>
        public void Close(DateTime timeUtc, decimal price, BacktestExitReason reason)
        {
            if (_position is not { } position)
                return;

            decimal exit = FuturesMath.ApplySlippage(price, position.Side, opening: false, _slippageRate);
            // Isolated: lỗ giá không vượt quá margin của lệnh (giống FuturesPosition.Close).
            decimal pnl = Math.Max(FuturesMath.UnrealizedPnl(position.Side, position.Quantity, position.EntryPrice,
                exit), -position.Margin);
            decimal fee = position.Quantity * exit * _feeRate;
            _cash += position.Margin + pnl - fee;
            Record(position, timeUtc, exit, pnl - fee, position.EntryFee + fee, reason);
        }

        /// <summary>Bị thanh lý: mất toàn bộ margin, không có phí đóng (giống FuturesPosition.Liquidate).</summary>
        private void Liquidate(DateTime timeUtc, decimal price)
        {
            if (_position is not { } position)
                return;
            Record(position, timeUtc, price, -position.Margin, position.EntryFee, BacktestExitReason.Liquidation);
        }

        private void Record(OpenPosition position, DateTime timeUtc, decimal exit, decimal pnlAfterExitFee,
            decimal fees, BacktestExitReason reason)
        {
            decimal netPnl = pnlAfterExitFee - position.EntryFee;
            Trades.Add(new BacktestTrade(position.EntryTimeUtc, position.EntryPrice, timeUtc, exit, position.Quantity,
                fees, netPnl, netPnl / position.Margin * 100, reason, position.Side));
            _position = null;
        }

        /// <summary>Tiền trong ví + margin và lãi/lỗ tạm tính của lệnh đang mở theo <paramref name="price"/>.</summary>
        public decimal EquityAt(decimal price) => _position is not { } position
            ? _cash
            : _cash + position.Margin + UnrealizedAt(position, price);

        /// <summary>Lệnh còn mở, định giá theo <paramref name="price"/>; null nếu đứng ngoài.</summary>
        public BacktestOpenTrade? OpenTradeAt(decimal price) => _position is not { } position
            ? null
            : new BacktestOpenTrade(position.EntryTimeUtc, position.EntryPrice, position.Quantity, position.EntryFee,
                price, UnrealizedAt(position, price) - position.EntryFee, position.Side);

        private static decimal UnrealizedAt(OpenPosition position, decimal price) =>
            Math.Max(FuturesMath.UnrealizedPnl(position.Side, position.Quantity, position.EntryPrice, price),
                -position.Margin);
    }

    /// <summary>Lệnh đang mở: chiều, giá vào, số coin, margin, phí mở và các mức thoát tính từ giá vào.</summary>
    private sealed record OpenPosition(
        PositionSide Side,
        DateTime EntryTimeUtc,
        decimal EntryPrice,
        decimal Quantity,
        decimal Margin,
        decimal EntryFee,
        decimal LiquidationPrice,
        decimal? TakeProfitPrice,
        decimal? StopLossPrice);
}
