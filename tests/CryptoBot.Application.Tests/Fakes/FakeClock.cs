using CryptoBot.Application.Abstractions;

namespace CryptoBot.Application.Tests;

internal sealed class FakeClock(DateTime now) : IClock
{
    public DateTime UtcNow { get; set; } = now;
}
