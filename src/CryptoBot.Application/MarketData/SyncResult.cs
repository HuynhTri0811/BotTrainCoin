using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.MarketData;

public sealed record SyncResult(
    CandleKey Key,
    int Inserted,
    int Updated,
    DateTime? LatestClosedOpenTimeUtc,
    DateTime ServerTimeUtc,
    IReadOnlyList<CandleIssue> Issues);
