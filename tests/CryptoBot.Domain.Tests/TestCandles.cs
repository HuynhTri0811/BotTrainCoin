using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

internal static class TestCandles
{
    public static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Candle At(int index, decimal close, bool closed = true, CandleInterval? interval = null)
    {
        interval ??= CandleInterval.H1;
        return new Candle("Binance", "BTCUSDT", interval, T0 + interval.Duration * index,
            close, close, close, close, 1m, closed, T0);
    }

    public static List<Candle> FromCloses(params decimal[] closes) =>
        closes.Select((close, index) => At(index, close)).ToList();
}
