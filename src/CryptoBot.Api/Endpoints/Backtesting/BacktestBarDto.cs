namespace CryptoBot.Api.Endpoints;

/// <summary>Time là Unix seconds (định dạng của Lightweight Charts). Equity null với cột thuộc phần warm-up.</summary>
public sealed record BacktestBarDto(long Time, decimal Open, decimal High, decimal Low, decimal Close,
    decimal? Equity);
