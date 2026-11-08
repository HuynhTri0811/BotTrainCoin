using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Signals;

public sealed record EvaluationResult(int Evaluated, IReadOnlyList<TradingSignal> NewSignals);
