using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record CreateRuleResult(AutoOrderRule? Rule, IReadOnlyList<string> Errors);
