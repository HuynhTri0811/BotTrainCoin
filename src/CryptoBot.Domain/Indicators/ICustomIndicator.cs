using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Indicators;

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
