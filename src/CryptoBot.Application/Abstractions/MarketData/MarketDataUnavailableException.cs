namespace CryptoBot.Application.Abstractions;

/// <summary>Sàn tạm thời không phục vụ dữ liệu (rate limit, lỗi mạng, 5xx).</summary>
public sealed class MarketDataUnavailableException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
