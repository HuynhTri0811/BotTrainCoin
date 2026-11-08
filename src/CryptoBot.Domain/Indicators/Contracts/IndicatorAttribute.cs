namespace CryptoBot.Domain.Indicators;

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
