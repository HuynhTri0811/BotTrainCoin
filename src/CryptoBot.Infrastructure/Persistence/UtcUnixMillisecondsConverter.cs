using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class UtcUnixMillisecondsConverter() : ValueConverter<DateTime, long>(
    value => ToUnixMs(value),
    value => DateTime.UnixEpoch.AddMilliseconds(value))
{
    private static long ToUnixMs(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Chỉ lưu DateTime UTC.");
        return (value.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
    }
}
