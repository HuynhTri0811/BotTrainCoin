namespace CryptoBot.Application.News;

/// <summary>Feed tạm thời không đọc được (lỗi mạng, 4xx/5xx, XML hỏng).</summary>
public sealed class NewsFeedUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
