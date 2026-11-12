using CryptoBot.Domain.Futures;

namespace CryptoBot.Domain.Backtesting;

/// <summary>Tín hiệu đọc được ở một nến.</summary>
/// <param name="Side">Long, Short, hoặc null khi không có tín hiệu.</param>
/// <param name="Reason">Lý do bằng chữ (chỉ cần khi có tín hiệu).</param>
public readonly record struct BacktestSignalReading(PositionSide? Side, string Reason)
{
    /// <summary>Không có tín hiệu.</summary>
    public static BacktestSignalReading None { get; } = new(null, "");
}
