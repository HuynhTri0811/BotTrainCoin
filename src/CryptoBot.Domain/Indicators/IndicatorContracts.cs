using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// Mô tả một đường để vẽ trên chart. Mỗi chỉ báo khai báo danh sách đường này qua <see cref="ICustomIndicator.Lines"/>.
/// </summary>
/// <param name="Key">
/// Khóa của đường. Phải trùng với khóa trong <see cref="IndicatorResult.Values"/> để chart biết lấy số nào vẽ đường nào.
/// Ví dụ <c>"rsi"</c>, <c>"ema"</c>.
/// </param>
/// <param name="Title">Tên hiển thị trên chart và chú thích, ví dụ <c>"RSI 14"</c>.</param>
/// <param name="Color">Màu của đường, dạng mã hex CSS, ví dụ <c>"#7c3aed"</c>.</param>
/// <param name="Overlay">
/// <c>true</c>: vẽ chung khung với nến, dùng cho đường có cùng thang giá (ví dụ MA, EMA).
/// <c>false</c>: vẽ ở khung riêng bên dưới, dùng cho đường có thang riêng (ví dụ RSI 0..100).
/// </param>
/// <param name="Levels">
/// Các mức tham chiếu nằm ngang vẽ kèm đường (ví dụ 30 và 70 của RSI). <c>null</c> nếu không cần.
/// </param>
public sealed record IndicatorLine(string Key, string Title, string Color, bool Overlay,
    IReadOnlyList<decimal>? Levels = null);

/// <summary>
/// Kết quả tính chỉ báo tại nến cuối của cửa sổ (tức nến vừa đóng).
/// </summary>
/// <param name="Values">
/// Các giá trị để vẽ lên chart, khóa trùng với <see cref="IndicatorLine.Key"/>.
/// Giá trị <c>null</c> nghĩa là chưa tính được (ví dụ đang warm-up), chart sẽ bỏ trống điểm đó.
/// </param>
/// <param name="Signal">Tín hiệu phát ra tại nến này: <see cref="IndicatorSignal.Long"/>, <see cref="IndicatorSignal.Short"/> hoặc <see cref="IndicatorSignal.None"/>.</param>
/// <param name="Reason">Câu giải thích bằng chữ vì sao có (hoặc không có) tín hiệu, hiển thị trên dashboard.</param>
public sealed record IndicatorResult(
    IReadOnlyDictionary<string, decimal?> Values,
    IndicatorSignal Signal,
    string Reason)
{
    /// <summary>
    /// Tạo nhanh một kết quả <b>không có tín hiệu</b> (<see cref="IndicatorSignal.None"/>).
    /// </summary>
    /// <param name="reason">Lý do không có tín hiệu, ví dụ <c>"Warm-up: có 50/200 nến."</c> hoặc <c>"RSI 47, không cắt mức."</c>.</param>
    /// <param name="values">
    /// Các giá trị vẫn muốn vẽ lên chart dù không có tín hiệu. Truyền <c>null</c> (mặc định) khi chưa có gì để vẽ,
    /// khi đó dùng dictionary rỗng.
    /// </param>
    /// <returns>Kết quả với <see cref="Signal"/> = <see cref="IndicatorSignal.None"/>.</returns>
    public static IndicatorResult None(string reason, IReadOnlyDictionary<string, decimal?>? values = null) =>
        new(values ?? new Dictionary<string, decimal?>(), IndicatorSignal.None, reason);
}

/// <summary>
/// Giao diện cho chỉ báo tự viết. Mỗi class implement giao diện này và gắn <see cref="IndicatorAttribute"/>
/// sẽ được tự động đăng ký khi ứng dụng khởi động.
/// </summary>
/// <remarks>
/// <para>
/// Tham số là các property public có setter. Giá trị được đọc từ <c>appsettings.json</c> → <c>Indicators</c> → <c>Parameters</c>,
/// tên khóa trùng tên property (ví dụ <c>"Period": 14</c> gán vào property <c>Period</c>).
/// </para>
/// <para>Quy ước bắt buộc (giống chiến lược EMA):</para>
/// <list type="bullet">
/// <item><description>Chỉ nhận nến <b>đã đóng</b>, sắp xếp tăng dần theo thời gian, phần tử cuối là nến đang đánh giá.</description></item>
/// <item><description>Thuần túy: không gọi API, không đọc đồng hồ, không lưu trạng thái vào field giữa các lần gọi.</description></item>
/// <item><description>Tính trên cửa sổ cố định <see cref="RequiredCandles"/> nến gần nhất → chart và lệnh tự động luôn cho cùng giá trị.</description></item>
/// </list>
/// <para>Thứ tự bot sử dụng: <see cref="Validate"/> (một lần lúc khởi động) → <see cref="Compute"/> (mỗi khi có nến mới đóng).</para>
/// <para>Ví dụ đầy đủ: <see cref="MyRsiIndicator"/>. Khung trống để bắt đầu: <see cref="TemplateIndicator"/>.</para>
/// </remarks>
public interface ICustomIndicator
{
    /// <summary>
    /// Số nến đã đóng cần có trước khi tính được. Bot chỉ gọi <see cref="Compute"/> khi đã có đủ số nến này,
    /// và luôn truyền đúng cửa sổ này để kết quả ổn định.
    /// </summary>
    int RequiredCandles { get; }

    /// <summary>
    /// Các đường hiển thị trên chart. Mỗi <see cref="IndicatorLine.Key"/> phải có giá trị tương ứng
    /// trong <see cref="IndicatorResult.Values"/> trả về từ <see cref="Compute"/>.
    /// </summary>
    IReadOnlyList<IndicatorLine> Lines { get; }

    /// <summary>
    /// Kiểm tra tham số sau khi đã đọc cấu hình. Gọi một lần lúc khởi động.
    /// </summary>
    /// <returns>
    /// Câu báo lỗi nếu tham số sai (chỉ báo sẽ bị bỏ qua và lỗi được ghi log), hoặc <c>null</c> nếu hợp lệ.
    /// </returns>
    string? Validate();

    /// <summary>
    /// Tính chỉ báo tại nến vừa đóng.
    /// </summary>
    /// <param name="closedCandles">
    /// Danh sách nến đã đóng, cũ → mới; <c>closedCandles[^1]</c> là nến vừa đóng.
    /// Không được sửa danh sách này.
    /// </param>
    /// <returns>
    /// Giá trị để vẽ, tín hiệu và lý do. Khi chưa đủ nến hoặc không có tín hiệu, dùng <see cref="IndicatorResult.None"/>.
    /// </returns>
    IndicatorResult Compute(IReadOnlyList<Candle> closedCandles);
}

/// <summary>
/// Đánh dấu một class <see cref="ICustomIndicator"/> để đăng ký và đặt tên loại cho nó.
/// Class không có attribute này sẽ không được đăng ký (ví dụ <see cref="TemplateIndicator"/>).
/// </summary>
/// <param name="type">Tên loại, dùng trong trường <c>"Type"</c> của cấu hình, ví dụ <c>"MyRsi"</c>. Phải duy nhất.</param>
/// <example><c>[Indicator("MyRsi", Description = "RSI cắt lên mức quá bán → Long")]</c></example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IndicatorAttribute(string type) : Attribute
{
    /// <summary>Tên loại chỉ báo, khớp với <c>"Type"</c> trong <c>appsettings.json</c>.</summary>
    public string Type { get; } = type;

    /// <summary>Mô tả ngắn hiển thị trên dashboard. Mặc định là chuỗi rỗng.</summary>
    public string Description { get; init; } = "";
}

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
