using System.Collections.Concurrent;

namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Cache <see cref="TimeZoneInfo"/> theo mã múi giờ IANA. Chỉ cache lần tìm thấy: máy thiếu dữ liệu múi giờ thì mỗi lần
/// gọi đều ném <see cref="TimeZoneNotFoundException"/>, không làm hỏng type initializer của cả tiến trình.
/// </summary>
internal static class TimeZoneCache
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Found = new();

    public static TimeZoneInfo Find(string timeZoneId) =>
        Found.GetOrAdd(timeZoneId, TimeZoneInfo.FindSystemTimeZoneById);
}
