namespace CryptoBot.Domain.Strategies;

public sealed record IndicatorValues(decimal FastPrev, decimal SlowPrev, decimal Fast, decimal Slow);
