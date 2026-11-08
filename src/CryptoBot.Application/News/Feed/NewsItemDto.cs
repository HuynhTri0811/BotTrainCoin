namespace CryptoBot.Application.News;

/// <summary>Một bài trong feed, chưa xử lý.</summary>
/// <param name="Id">guid của bài, không có thì là link.</param>
/// <param name="Description">Tóm tắt, có thể chứa HTML.</param>
public sealed record NewsItemDto(string Id, string Title, string Url, string? Description, DateTime PublishedAtUtc);
