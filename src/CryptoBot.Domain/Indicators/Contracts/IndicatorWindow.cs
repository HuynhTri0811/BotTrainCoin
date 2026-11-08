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
        if (candles.Count < count)
            throw new ArgumentException($"Cần {count} nến, chỉ có {candles.Count}.", nameof(candles));
        decimal[] closes = new decimal[count];
        // Vị trí bắt đầu: bỏ qua các nến cũ hơn cửa sổ.
        int offset = candles.Count - count;
        for (int index = 0; index < count; index++)
            closes[index] = candles[offset + index].Close;
        return closes;
    }
}
