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
    public int Period { get; set; } = 20;

    public int RequiredCandles => Period + 1;

    public string? Validate() => Period < 1 ? "Period phải >= 1." : null;

    // TODO: các đường muốn vẽ. Overlay = true: vẽ trên nến; false: khung riêng bên dưới.
    public IReadOnlyList<IndicatorLine> Lines => [new("high", $"Đỉnh {Period}", "#ea580c", Overlay: true)];

    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        if (closedCandles.Count < RequiredCandles)
            return IndicatorResult.None("Chưa đủ nến.");

        // TODO: công thức của bạn. closedCandles[^1] là nến vừa đóng; mỗi nến có Open/High/Low/Close/Volume.
        Candle current = closedCandles[^1];
        List<Candle> previous = closedCandles.Skip(closedCandles.Count - 1 - Period).Take(Period).ToList();
        decimal highest = previous.Max(c => c.High);

        Dictionary<string, decimal?> values = new Dictionary<string, decimal?> { ["high"] = highest };
        if (current.Close > highest)
            return new IndicatorResult(values, IndicatorSignal.Long, $"Đóng cửa {current.Close} vượt đỉnh {Period} nến {highest}.");
        return IndicatorResult.None("Chưa vượt đỉnh.", values);
    }
}
