using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// Các hàm tiện ích để lấy dữ liệu từ cửa sổ nến, dùng chung cho mọi chỉ báo.
/// </summary>
public static class IndicatorWindow
{
    /// <summary>Lấy giá đóng cửa của <paramref name="count"/> nến cuối, tăng dần theo thời gian.</summary>
    /// <param name="candles">Danh sách nến đã đóng, cũ → mới.</param>
    /// <param name="count">Số nến cuối cần lấy.</param>
    /// <returns>
    /// Mảng mới gồm <paramref name="count"/> giá đóng cửa; phần tử cuối là giá đóng cửa của nến vừa đóng.
    /// </returns>
    /// <exception cref="ArgumentException">Khi <paramref name="candles"/> có ít hơn <paramref name="count"/> nến.</exception>
    /// <example>Nến có giá đóng cửa [10, 11, 12, 13], <c>count</c> = 2 → [12, 13].</example>
    public static decimal[] LastCloses(IReadOnlyList<Candle> candles, int count)
    {
        decimal[] closes = new decimal[count];
        CopyLastCloses(candles, closes);
        return closes;
    }

    /// <summary>
    /// Ghi giá đóng cửa của <c>destination.Length</c> nến cuối vào <paramref name="destination"/>, tăng dần theo thời
    /// gian. Dùng khi chỉ báo tự giữ bộ đệm (ví dụ mượn từ <see cref="System.Buffers.ArrayPool{T}"/>) để không cấp
    /// phát mảng mới mỗi nến.
    /// </summary>
    /// <param name="candles">Danh sách nến đã đóng, cũ → mới.</param>
    /// <param name="destination">Chỗ ghi; độ dài là số nến cuối cần lấy.</param>
    /// <exception cref="ArgumentException">Khi <paramref name="candles"/> có ít nến hơn độ dài chỗ ghi.</exception>
    public static void CopyLastCloses(IReadOnlyList<Candle> candles, Span<decimal> destination)
    {
        int count = destination.Length;
        if (candles.Count < count)
            throw new ArgumentException($"Cần {count} nến, chỉ có {candles.Count}.", nameof(candles));
        // Vị trí bắt đầu: bỏ qua các nến cũ hơn cửa sổ.
        int offset = candles.Count - count;
        for (int index = 0; index < count; index++)
            destination[index] = candles[offset + index].Close;
    }
}
