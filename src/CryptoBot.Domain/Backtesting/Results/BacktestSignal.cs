using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Backtesting;

/// <summary>Tín hiệu BUY/SELL chiến lược phát ra trong backtest, đúng như bot sẽ ghi vào bảng Signals.</summary>
/// <param name="CandleOpenTimeUtc">Giờ mở của nến phát tín hiệu (tín hiệu có lúc nến này đóng).</param>
/// <param name="Action">Buy hoặc Sell.</param>
/// <param name="Close">Giá đóng cửa của nến đó.</param>
/// <param name="Reason">Lý do bằng chữ của chiến lược.</param>
public sealed record BacktestSignal(DateTime CandleOpenTimeUtc, SignalAction Action, decimal Close, string Reason);
