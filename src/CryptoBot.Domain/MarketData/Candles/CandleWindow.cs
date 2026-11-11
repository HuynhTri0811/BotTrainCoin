namespace CryptoBot.Domain.MarketData;

/// <summary>Kiểm tra một cửa sổ nến trước khi đưa vào chiến lược.</summary>
public static class CandleWindow
{
    /// <summary>
    /// Cửa sổ có nến thiếu: khoảng cách từ nến đầu đến nến cuối khác (số nến − 1) × khung nến.
    /// Cửa sổ có khoảng thiếu thì không đánh giá (nguyên tắc C3), cả khi chạy thật lẫn khi backtest.
    /// </summary>
    /// <param name="window">Nến tăng dần theo thời gian.</param>
    /// <param name="interval">Khung nến của cửa sổ.</param>
    /// <example>3 nến 1h mở lúc 00:00, 01:00, 03:00 → 3h ≠ 2 × 1h → true.</example>
    public static bool HasGap(IReadOnlyList<Candle> window, CandleInterval interval) =>
        window.Count > 1 &&
        window[^1].OpenTimeUtc - window[0].OpenTimeUtc != interval.Duration * (window.Count - 1);
}
