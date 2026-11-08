using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Signals;

public sealed record ChartData(StrategyInstance Instance, EmaCrossParameters Parameters,
    IReadOnlyList<ChartPoint> Points, IReadOnlyList<TradingSignal> Signals);
