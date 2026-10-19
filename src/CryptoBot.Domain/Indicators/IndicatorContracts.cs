using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Indicators;

/// <summary>Tín hiệu một chỉ báo phát ra tại một nến đã đóng.</summary>
public enum IndicatorSignal
{
    None = 0,
    Long = 1,
    Short = 2,
}

/// <summary>
/// Mô tả một đường để vẽ trên chart. <see cref="Key"/> trùng với khóa trong <see cref="IndicatorResult.Values"/>.
/// <see cref="Overlay"/> = true: vẽ chung với nến (ví dụ đường MA); false: vẽ ở khung riêng bên dưới (ví dụ RSI).
/// <see cref="Levels"/>: các mức tham chiếu nằm ngang (ví dụ 30/70 của RSI).
/// </summary>
public sealed record IndicatorLine(string Key, string Title, string Color, bool Overlay,
    IReadOnlyList<decimal>? Levels = null);

/// <summary>Kết quả tính chỉ báo tại nến cuối của cửa sổ.</summary>
public sealed record IndicatorResult(
    IReadOnlyDictionary<string, decimal?> Values,
    IndicatorSignal Signal,
    string Reason)
{
    public static IndicatorResult None(string reason, IReadOnlyDictionary<string, decimal?>? values = null) =>
        new(values ?? new Dictionary<string, decimal?>(), IndicatorSignal.None, reason);
}

/// <summary>
/// Chỉ báo tự viết. Tham số là các property public có setter (đọc từ <c>appsettings.json</c> → <c>Indicators</c>).
/// Quy ước giống chiến lược EMA:
/// <list type="bullet">
/// <item>Chỉ nhận nến đã đóng, sắp tăng dần, phần tử cuối là nến đang đánh giá.</item>
/// <item>Thuần túy: không gọi API, không đọc đồng hồ, không lưu trạng thái vào field giữa các lần gọi.</item>
/// <item>Tính trên cửa sổ cố định <see cref="RequiredCandles"/> nến gần nhất → chart và lệnh tự động cho cùng giá trị.</item>
/// </list>
/// Đăng ký bằng <see cref="IndicatorAttribute"/>.
/// </summary>
public interface ICustomIndicator
{
    /// <summary>Số nến đã đóng cần có trước khi tính được.</summary>
    int RequiredCandles { get; }

    /// <summary>Các đường hiển thị trên chart.</summary>
    IReadOnlyList<IndicatorLine> Lines { get; }

    /// <summary>Kiểm tra tham số; trả về lý do nếu sai, null nếu hợp lệ.</summary>
    string? Validate();

    IndicatorResult Compute(IReadOnlyList<Candle> closedCandles);
}

/// <summary>Đặt tên loại cho chỉ báo; tên này dùng trong <c>"Type"</c> của cấu hình.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IndicatorAttribute(string type) : Attribute
{
    public string Type { get; } = type;

    /// <summary>Mô tả ngắn hiển thị trên dashboard.</summary>
    public string Description { get; init; } = "";
}

public static class IndicatorWindow
{
    /// <summary>Giá đóng cửa của <paramref name="count"/> nến cuối, tăng dần theo thời gian.</summary>
    public static decimal[] LastCloses(IReadOnlyList<Candle> candles, int count)
    {
        if (candles.Count < count)
            throw new ArgumentException($"Cần {count} nến, chỉ có {candles.Count}.", nameof(candles));
        var closes = new decimal[count];
        var offset = candles.Count - count;
        for (var i = 0; i < count; i++)
            closes[i] = candles[offset + i].Close;
        return closes;
    }
}
