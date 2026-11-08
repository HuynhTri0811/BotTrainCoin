using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record AutoOrderSummary(
    IReadOnlyList<AutoOrderRule> Rules,
    AutoOrderOptions Defaults,
    DailyRiskState Risk);
