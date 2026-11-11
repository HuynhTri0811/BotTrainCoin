using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

/// <summary>Điểm vào/ra lệnh, gắn vào cột chứa nến khớp lệnh.</summary>
/// <param name="BarOpenTimeUtc">Giờ của cột chứa lệnh (khớp với <see cref="BacktestChartBar.OpenTimeUtc"/>).</param>
/// <param name="IsEntry">true = vào lệnh, false = thoát lệnh.</param>
/// <param name="Price">Giá khớp.</param>
/// <param name="ExitReason">Lý do thoát; null với điểm vào.</param>
/// <param name="NetPnl">Lãi/lỗ sau phí của lệnh; null với điểm vào.</param>
public sealed record BacktestChartMarker(
    DateTime BarOpenTimeUtc,
    bool IsEntry,
    decimal Price,
    BacktestExitReason? ExitReason,
    decimal? NetPnl);
