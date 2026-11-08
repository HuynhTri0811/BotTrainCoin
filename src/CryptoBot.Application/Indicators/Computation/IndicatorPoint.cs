using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Indicators;

public sealed record IndicatorPoint(Candle Candle, IndicatorResult Result);
