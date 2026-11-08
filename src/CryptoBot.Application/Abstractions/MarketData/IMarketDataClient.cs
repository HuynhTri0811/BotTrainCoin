using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Abstractions;

public interface IMarketDataClient
{
    string Exchange { get; }

    Task<DateTime> GetServerTimeAsync(CancellationToken ct);

    /// <summary>Giá giao dịch gần nhất (Spot) của symbol.</summary>
    Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct);

    /// <summary>Klines tăng dần theo OpenTime, bắt đầu từ <paramref name="startTimeUtc"/> (bao gồm).</summary>
    Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct);
}
