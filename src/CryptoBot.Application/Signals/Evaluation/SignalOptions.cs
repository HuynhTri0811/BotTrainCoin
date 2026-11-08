namespace CryptoBot.Application.Signals;

public sealed class SignalOptions
{
    /// <summary>
    /// Signal được coi là mới (không phải backfill) nếu được tạo trong khoảng này sau khi nến đóng.
    /// Chỉ signal mới mới có thể được dùng để đặt lệnh ở các giai đoạn sau.
    /// </summary>
    public TimeSpan MaxFreshAge { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Số nến đóng tối đa đánh giá mỗi lần chạy.</summary>
    public int MaxCandlesPerRun { get; set; } = 2000;
}
