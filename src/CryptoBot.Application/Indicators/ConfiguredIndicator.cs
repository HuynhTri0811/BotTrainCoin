using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Indicators;

namespace CryptoBot.Application.Indicators;

public sealed record ConfiguredIndicator(
    string Name,
    string Type,
    string Description,
    CandleKey Key,
    ICustomIndicator Indicator);
