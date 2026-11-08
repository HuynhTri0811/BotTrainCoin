using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Abstractions;

public sealed record CandleKey(string Exchange, string Symbol, CandleInterval Interval)
{
    public override string ToString() => $"{Exchange}:{Symbol}:{Interval.Code}";
}
