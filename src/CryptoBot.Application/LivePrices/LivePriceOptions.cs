namespace CryptoBot.Application.LivePrices;

public sealed class LivePriceOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Số lần lấy giá giữ lại trong bộ nhớ cho mỗi symbol (240 × 15s = 1 giờ).</summary>
    public int HistorySize { get; set; } = 240;

    /// <summary>
    /// Mặc định để trống: binder của .NET cộng thêm phần tử từ cấu hình vào danh sách mặc định
    /// (gây trùng symbol), nên giá trị lấy hoàn toàn từ appsettings.json.
    /// </summary>
    public List<string> Symbols { get; set; } = [];
}
