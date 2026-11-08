using CryptoBot.Application.Abstractions;

namespace CryptoBot.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
