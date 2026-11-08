using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Abstractions;

/// <summary>Một kline thô từ sàn. Các cột thống kê để null khi sàn không trả (ví dụ Bitfinex).</summary>
public sealed record KlineDto(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal? QuoteVolume = null,
    long? TradeCount = null,
    decimal? TakerBuyVolume = null)
{
    /// <summary>
    /// Nến của <paramref name="key"/> nhận lúc <paramref name="receivedAtUtc"/> (giờ sàn nếu có). Nến đã đóng khi
    /// thời điểm nhận ≥ OpenTime + một khung.
    /// </summary>
    public Candle ToCandle(CandleKey key, DateTime receivedAtUtc) =>
        new(key.Exchange, key.Symbol, key.Interval, OpenTimeUtc, Open, High, Low, Close, Volume,
            isClosed: receivedAtUtc >= OpenTimeUtc + key.Interval.Duration,
            receivedAtUtc: receivedAtUtc, quoteVolume: QuoteVolume, tradeCount: TradeCount,
            takerBuyVolume: TakerBuyVolume);
}
