using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed class FuturesSymbolOptions
{
    public string Symbol { get; set; } = "";
    public decimal QuantityStep { get; set; }
    public decimal TickSize { get; set; }
    public decimal MinNotional { get; set; }
    public decimal MaintenanceMarginRate { get; set; }

    public FuturesSymbolRules ToRules() => new(Symbol, QuantityStep, TickSize, MinNotional, MaintenanceMarginRate);
}
