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
