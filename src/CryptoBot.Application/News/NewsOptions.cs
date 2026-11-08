namespace CryptoBot.Application.News;

public sealed class NewsOptions
{
    /// <summary>Chu kỳ đồng bộ tin. Feed chỉ giữ 20–40 bài mới nhất, nên chu kỳ quá dài có thể lỡ bài.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(5);

    /// <summary>Tóm tắt dài hơn bị cắt (chỉ lưu tóm tắt ngắn, đọc bài gốc qua link).</summary>
    public int MaxSummaryLength { get; set; } = 500;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public List<NewsFeedOptions> Feeds { get; set; } = [];
}
