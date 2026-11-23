namespace CryptoBot.Application.Futures;

/// <summary>
/// Singleton tuần tự hóa việc hủy và việc kích hoạt quy tắc tự mở lệnh giữa các request/worker (mỗi bên một
/// DbContext). Thứ tự khóa luôn là khóa này rồi mới đến <see cref="PaperFuturesLock"/>.
/// </summary>
public sealed class AutoOrderLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}
