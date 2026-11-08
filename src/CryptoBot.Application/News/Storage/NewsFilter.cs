namespace CryptoBot.Application.News;

/// <summary>Điều kiện lọc tin; trường null là không lọc. <see cref="Search"/> tìm trong tiêu đề và tóm tắt.</summary>
public sealed record NewsFilter(
    string? Coin = null,
    string? Source = null,
    string? Search = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);
