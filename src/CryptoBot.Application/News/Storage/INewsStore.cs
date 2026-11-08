using CryptoBot.Domain.News;

namespace CryptoBot.Application.News;

public interface INewsStore
{
    /// <summary>Những mã trong <paramref name="externalIds"/> đã được lưu cho <paramref name="source"/>.</summary>
    Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct);

    /// <summary>
    /// Tối đa <paramref name="limit"/> bài mới nhất (theo giờ đăng), mới → cũ, khớp <paramref name="filter"/>.
    /// Khoảng thời gian là [FromUtc, ToUtc).
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct);

    void AddRange(IEnumerable<NewsArticle> articles);
    Task SaveAsync(CancellationToken ct);
}
