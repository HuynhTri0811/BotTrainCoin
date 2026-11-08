using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Abstractions;

public sealed record UpsertResult(int Inserted, int Updated, int Unchanged, IReadOnlyList<CandleIssue> Conflicts);
