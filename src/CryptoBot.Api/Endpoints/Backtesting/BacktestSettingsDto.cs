namespace CryptoBot.Api.Endpoints;

/// <summary>Vốn/rủi ro đã dùng. Sizing: PercentOfEquity hoặc FixedAmount; TP/SL null = không dùng.</summary>
public sealed record BacktestSettingsDto(decimal InitialCapital, string Sizing, decimal SizeValue,
    decimal? TakeProfitPercent, decimal? StopLossPercent, decimal FeePercent);
