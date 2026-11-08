namespace CryptoBot.Application.News;

public interface INewsFeedClient
{
    /// <summary>Các bài trong feed ở <paramref name="url"/>; bài thiếu tiêu đề hoặc ngày đăng bị bỏ qua.</summary>
    Task<IReadOnlyList<NewsItemDto>> GetItemsAsync(string url, CancellationToken ct);
}
