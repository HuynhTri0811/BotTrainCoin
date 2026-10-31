namespace CryptoBot.Domain.Common;

/// <summary>Công thức % thay đổi giá dùng chung cho các phân tích giá.</summary>
internal static class PercentChange
{
    /// <summary>% thay đổi từ <paramref name="from"/> đến <paramref name="to"/>, ví dụ 100 → 101 là 1.</summary>
    public static decimal Between(decimal from, decimal to) => (to / from - 1) * 100;

    /// <summary>Tỷ lệ thay đổi dương (giá tăng thật, không tính đứng yên) trên tổng số, theo %.</summary>
    public static decimal UpShare(IReadOnlyCollection<decimal> changes) =>
        (decimal)changes.Count(change => change > 0) / changes.Count * 100;
}
