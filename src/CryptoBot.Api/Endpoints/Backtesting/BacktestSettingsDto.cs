namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Vốn/rủi ro đã dùng. Sizing: PercentOfEquity hoặc FixedAmount (Futures: margin mỗi lệnh); TP/SL null = không dùng.
/// Mode: Spot hoặc Futures; Futures null với Spot.
/// </summary>
public sealed record BacktestSettingsDto(decimal InitialCapital, string Sizing, decimal SizeValue,
    decimal? TakeProfitPercent, decimal? StopLossPercent, decimal FeePercent, string Mode,
    BacktestFuturesDto? Futures);

/// <summary>Thiết lập Futures. Direction: LongOnly, ShortOnly hoặc Both.</summary>
public sealed record BacktestFuturesDto(int Leverage, string Direction, bool CloseOnReverse, decimal SlippagePercent,
    decimal MaintenanceMarginPercent);
