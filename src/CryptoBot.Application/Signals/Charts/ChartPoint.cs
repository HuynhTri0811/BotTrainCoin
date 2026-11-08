using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Signals;

public sealed record ChartPoint(Candle Candle, decimal? FastEma, decimal? SlowEma);
