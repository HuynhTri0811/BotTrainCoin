namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Điểm vào/ra lệnh trên chart; Time trùng với Time của cột chứa nến khớp lệnh. Kind: Entry hoặc Exit.
/// ExitReason và NetPnl null với điểm vào.
/// </summary>
public sealed record BacktestMarkerDto(long Time, string Kind, decimal Price, string? ExitReason, decimal? NetPnl);
