using CryptoBot.Application.Abstractions;

namespace CryptoBot.IntegrationTests;

internal sealed class TestClock(DateTime now) : IClock
{
    public DateTime UtcNow { get; set; } = now;
}
