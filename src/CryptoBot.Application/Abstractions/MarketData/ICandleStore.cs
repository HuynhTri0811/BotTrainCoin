using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Abstractions;

public interface ICandleStore
{
    Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct);

    /// <summary>OpenTime sớm nhất (từ <paramref name="fromUtc"/>) của nến chưa có số giao dịch hoặc khối lượng mua chủ động; null nếu đủ.</summary>
    Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc, CancellationToken ct);

    /// <summary>
    /// Thêm/cập nhật một lô nến. Các lần gọi cùng khóa nến chạy lần lượt (kể cả từ DbContext khác), nên các bên ghi
    /// chồng khoảng nhau không cùng chèn một nến.
    /// </summary>
    Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> nến mới nhất có OpenTime &lt; before (nếu có), trả về tăng dần.</summary>
    Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> nến đầu tiên có OpenTime &gt; after (nếu có), tăng dần.</summary>
    Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct);
}
