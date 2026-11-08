namespace CryptoBot.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
