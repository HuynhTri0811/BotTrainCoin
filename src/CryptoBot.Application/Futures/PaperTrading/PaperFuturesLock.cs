namespace CryptoBot.Application.Futures;

/// <summary>Mọi thay đổi số dư/vị thế paper futures đi qua một khóa: API và worker không ghi chồng nhau.</summary>
public sealed class PaperFuturesLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}
