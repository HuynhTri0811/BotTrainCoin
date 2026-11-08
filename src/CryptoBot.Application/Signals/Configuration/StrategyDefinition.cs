using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Signals;

public sealed class StrategyDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = StrategyTypes.EmaCross;
    public string Exchange { get; set; } = "Binance";
    public string Symbol { get; set; } = "";
    public string Interval { get; set; } = "1h";
    public bool Enabled { get; set; } = true;
    public EmaCrossParameters Parameters { get; set; } = new();
}
