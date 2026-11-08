using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// KHUNG TRỐNG để bạn bắt đầu chỉ báo mới:
///   1. Copy file này thành tên mới, ví dụ <c>MyBreakoutIndicator.cs</c>, đổi tên class.
///   2. Bỏ comment dòng [Indicator(...)] và đặt tên loại, ví dụ "MyBreakout".
///   3. Sửa tham số, Lines và Compute (các chỗ TODO).
///   4. Thêm vào appsettings.json → "Indicators": { "Name": "...", "Type": "MyBreakout", "Symbol": "BTCUSDT", "Interval": "15m" }.
///   5. Restart API. Chỉ báo hiện trong dropdown trên chart và trong "Tự mở lệnh theo giá" → "Chỉ báo".
/// Xem <see cref="MyRsiIndicator"/> để có ví dụ đầy đủ.
///
/// Class này KHÔNG có [Indicator] nên không được đăng ký; ví dụ dưới đây: giá đóng cửa vượt đỉnh N nến trước → Long.
/// </summary>
// [Indicator("MyTemplate", Description = "Mô tả ngắn chỉ báo của bạn")]
public sealed class TemplateIndicator : ICustomIndicator
{
    // TODO: tham số của bạn (property public có setter).
    /// <summary>Số nến trước đó dùng để tìm đỉnh cao nhất. Mặc định 20.</summary>
    public int Period { get; set; } = 20;

    /// <summary><see cref="Period"/> nến để tìm đỉnh + 1 nến vừa đóng để so sánh.</summary>
    public int RequiredCandles => Period + 1;

    /// <summary>Hợp lệ khi <see cref="Period"/> &gt;= 1.</summary>
    /// <returns>Câu báo lỗi, hoặc <c>null</c> nếu hợp lệ.</returns>
    public string? Validate() => Period < 1 ? "Period phải >= 1." : null;

    // TODO: các đường muốn vẽ. Overlay = true: vẽ trên nến; false: khung riêng bên dưới.
    /// <summary>Một đường "high" (đỉnh <see cref="Period"/> nến trước) vẽ chung khung với nến.</summary>
    public IReadOnlyList<IndicatorLine> Lines => [new("high", $"Đỉnh {Period}", "#ea580c", Overlay: true)];

    /// <summary>
    /// Tìm đỉnh cao nhất (High) của <see cref="Period"/> nến <b>trước</b> nến vừa đóng,
    /// rồi so với giá đóng cửa của nến vừa đóng.
    /// </summary>
    /// <param name="closedCandles">Nến đã đóng, cũ → mới; phần tử cuối là nến vừa đóng.</param>
    /// <returns>
    /// Long nếu giá đóng cửa vượt đỉnh; ngược lại None. Luôn trả về giá trị "high" để vẽ (trừ khi chưa đủ nến).
    /// </returns>
    /// <example>Period 3, High của 3 nến trước [105, 110, 108], nến vừa đóng Close 112 → Long (112 &gt; 110).</example>
    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        if (closedCandles.Count < RequiredCandles)
            return IndicatorResult.None("Chưa đủ nến.");

        // TODO: công thức của bạn. closedCandles[^1] là nến vừa đóng; mỗi nến có Open/High/Low/Close/Volume.
        Candle current = closedCandles[^1];
        List<Candle> previous = closedCandles.Skip(closedCandles.Count - 1 - Period).Take(Period).ToList();
        decimal highest = previous.Max(candle => candle.High);

        Dictionary<string, decimal?> values = new Dictionary<string, decimal?> { ["high"] = highest };
        if (current.Close > highest)
            return new IndicatorResult(values, IndicatorSignal.Long, $"Đóng cửa {current.Close} vượt đỉnh {Period} nến {highest}.");
        return IndicatorResult.None("Chưa vượt đỉnh.", values);
    }
}
