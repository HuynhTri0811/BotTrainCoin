namespace CryptoBot.Domain.News;

/// <summary>
/// Một bài tin tức lấy từ RSS của một nguồn. Khóa duy nhất: (Source, ExternalId), nên đồng bộ lại không lưu trùng.
/// Bot chỉ lưu tiêu đề, tóm tắt ngắn và link tới bài gốc.
/// </summary>
public sealed class NewsArticle
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Tên nguồn trong cấu hình, ví dụ "CoinDesk".</summary>
    public string Source { get; private set; } = null!;

    /// <summary>Mã bài của nguồn (guid trong RSS, không có thì là link).</summary>
    public string ExternalId { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    /// <summary>Link tới bài gốc.</summary>
    public string Url { get; private set; } = null!;

    /// <summary>Tóm tắt dạng chữ thường (đã bỏ HTML), có thể rỗng.</summary>
    public string Summary { get; private set; } = null!;

    /// <summary>Coin được nhắc tới, cách nhau bằng dấu phẩy, ví dụ "BTC,ETH"; rỗng nếu không có.</summary>
    public string Coins { get; private set; } = null!;

    /// <summary>Giờ nguồn đăng bài, UTC.</summary>
    public DateTime PublishedAtUtc { get; private set; }

    /// <summary>Giờ bot lấy bài về, UTC.</summary>
    public DateTime FetchedAtUtc { get; private set; }

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private NewsArticle() { }

    public NewsArticle(string source, string externalId, string title, string url, string summary,
        IReadOnlyList<string> coins, DateTime publishedAtUtc, DateTime fetchedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Thiếu nguồn.", nameof(source));
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("Thiếu mã bài.", nameof(externalId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Thiếu tiêu đề.", nameof(title));
        if (publishedAtUtc.Kind != DateTimeKind.Utc || fetchedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời gian phải là UTC.", nameof(publishedAtUtc));

        Source = source;
        ExternalId = externalId;
        Title = title;
        Url = url;
        Summary = summary;
        Coins = string.Join(',', coins);
        PublishedAtUtc = publishedAtUtc;
        FetchedAtUtc = fetchedAtUtc;
    }
}
