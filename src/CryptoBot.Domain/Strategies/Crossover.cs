namespace CryptoBot.Domain.Strategies;

/// <summary>Phát hiện giao cắt giữa nến trước và nến hiện tại. Dùng chung cho chiến lược và chỉ báo.</summary>
public static class Crossover
{
    /// <summary>Đường A cắt lên đường B: trước đó A ≤ B, bây giờ A &gt; B.</summary>
    public static bool Up(decimal aPrev, decimal bPrev, decimal a, decimal b) => aPrev <= bPrev && a > b;

    /// <summary>Đường A cắt xuống đường B: trước đó A ≥ B, bây giờ A &lt; B.</summary>
    public static bool Down(decimal aPrev, decimal bPrev, decimal a, decimal b) => aPrev >= bPrev && a < b;

    /// <summary>Giá trị cắt lên qua một mức cố định (ví dụ RSI vượt lên 30).</summary>
    public static bool UpThrough(decimal prev, decimal now, decimal level) => Up(prev, level, now, level);

    /// <summary>Giá trị cắt xuống qua một mức cố định (ví dụ RSI rơi xuống dưới 70).</summary>
    public static bool DownThrough(decimal prev, decimal now, decimal level) => Down(prev, level, now, level);
}
