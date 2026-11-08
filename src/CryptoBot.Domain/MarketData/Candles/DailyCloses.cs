namespace CryptoBot.Domain.MarketData;

/// <summary>Quy tắc chung cho chuỗi giá đóng cửa theo ngày mà các dự báo theo ngày nhận vào.</summary>
internal static class DailyCloses
{
    /// <summary>Số ngày phía trước phải lớn hơn 0 và mọi giá đóng cửa phải dương.</summary>
    public static void Validate(IReadOnlyList<decimal> dailyCloses, int horizonDays)
    {
        if (horizonDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(horizonDays), "Số ngày phải lớn hơn 0.");
        if (dailyCloses.Any(close => close <= 0))
            throw new ArgumentException("Giá đóng cửa phải dương.", nameof(dailyCloses));
    }
}
