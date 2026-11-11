namespace CryptoBot.Domain.Backtesting;

/// <summary>Tổng vốn (tiền mặt + coin đang giữ theo giá đóng cửa) tại một nến.</summary>
public sealed record EquityPoint(DateTime CandleOpenTimeUtc, decimal Equity);
