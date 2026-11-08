namespace CryptoBot.Application.News;

/// <summary>Một nguồn tin RSS công khai (không cần API key).</summary>
public sealed class NewsFeedOptions
{
    /// <summary>Tên hiển thị và lưu cùng bài, ví dụ "CoinDesk". Đổi tên = nguồn mới.</summary>
    public string Name { get; set; } = "";

    public string Url { get; set; } = "";
}
