namespace CryptoBot.Domain.Strategies;

/// <summary>
/// Phát hiện giao cắt giữa nến trước và nến hiện tại. Dùng chung cho chiến lược và chỉ báo.
///
/// Vì sao cần cả giá trị "trước" lẫn "bây giờ": tín hiệu chỉ nên phát ĐÚNG MỘT LẦN tại nến mà hai đường
/// vừa đổi vị trí. Nếu chỉ hỏi "A có đang trên B không" thì mọi nến sau đó cũng đều đúng và bot sẽ phát
/// tín hiệu lặp đi lặp lại.
/// </summary>
public static class Crossover
{
    /// <summary>Đường A cắt lên đường B: trước đó A ≤ B, bây giờ A &gt; B.</summary>
    /// <example>EMA9 nến trước 100 ≤ EMA21 101, nến này EMA9 103 &gt; EMA21 102 → true.</example>
    public static bool Up(decimal aPrev, decimal bPrev, decimal a, decimal b) => aPrev <= bPrev && a > b;

    /// <summary>Đường A cắt xuống đường B: trước đó A ≥ B, bây giờ A &lt; B.</summary>
    /// <example>EMA9 nến trước 103 ≥ EMA21 102, nến này EMA9 100 &lt; EMA21 101 → true.</example>
    public static bool Down(decimal aPrev, decimal bPrev, decimal a, decimal b) => aPrev >= bPrev && a < b;

    /// <summary>Giá trị cắt lên qua một mức cố định (ví dụ RSI vượt lên 30).</summary>
    /// <remarks>Mức cố định coi như một đường nằm ngang, nên dùng lại <see cref="Up"/> với B = mức.</remarks>
    /// <example>RSI 28.4 → 31.2, mức 30 → true; RSI 31 → 35 → false (đã ở trên 30 từ trước).</example>
    public static bool UpThrough(decimal prev, decimal now, decimal level) => Up(prev, level, now, level);

    /// <summary>Giá trị cắt xuống qua một mức cố định (ví dụ RSI rơi xuống dưới 70).</summary>
    /// <example>RSI 72 → 68, mức 70 → true.</example>
    public static bool DownThrough(decimal prev, decimal now, decimal level) => Down(prev, level, now, level);
}
