using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed class PaperFuturesOptions
{
    public decimal StartingBalance { get; set; } = 1000m;
    public decimal TakerFeeRate { get; set; } = 0.0005m;
    public decimal SlippageRate { get; set; } = 0.0005m;
    public int MaxLeverage { get; set; } = 10;
    public TimeSpan MarkPriceMaxAge { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan MarkPollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public List<FuturesSymbolOptions> Symbols { get; set; } = [];

    public FuturesSymbolRules? FindRules(string symbol) =>
        Symbols.FirstOrDefault(symbolOptions => symbolOptions.Symbol == symbol)?.ToRules();
}
