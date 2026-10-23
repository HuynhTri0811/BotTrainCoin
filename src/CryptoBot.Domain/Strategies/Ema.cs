namespace CryptoBot.Domain.Strategies;

/// <summary>
/// EMA (Exponential Moving Average, đường trung bình động hàm mũ).
///
/// Khác trung bình cộng thường (SMA) ở chỗ: nến càng gần đây càng được tính nặng hơn,
/// nên EMA bám theo giá nhanh hơn SMA.
///
/// Công thức cho từng nến:  EMA_mới = alpha × Giá_đóng_cửa + (1 − alpha) × EMA_cũ,  với alpha = 2 / (period + 1).
/// Ví dụ period = 9 → alpha = 0.2: EMA mới lấy 20% giá mới + 80% EMA cũ.
///
/// Giá trị EMA đầu tiên (seed) = trung bình cộng (SMA) của <c>period</c> giá đầu tiên.
/// </summary>
public static class Ema
{
    /// <summary>
    /// Tính EMA cho chuỗi giá trị. Seed là SMA của <paramref name="period"/> phần tử đầu;
    /// các vị trí trước seed trả về null. Cùng một cách khởi tạo cho live, paper và backtest.
    /// </summary>
    /// <example>
    /// Giá [2, 4, 6, 8, 10], period 3 → [null, null, 4, 6, 8]
    /// (seed = (2+4+6)/3 = 4; alpha = 0.5; 0.5×8 + 0.5×4 = 6; 0.5×10 + 0.5×6 = 8).
    /// </example>
    public static decimal?[] Calculate(IReadOnlyList<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        decimal?[] result = new decimal?[values.Count];
        // Chưa đủ period giá thì chưa có EMA nào: trả về toàn null.
        if (values.Count < period)
            return result;

        ReadOnlySpan<decimal> span = values as decimal[] ?? values.ToArray();
        decimal ema = Seed(span, period);
        result[period - 1] = ema;

        (decimal alpha, decimal keep) = Weights(period);
        // Từ giá thứ period trở đi, mỗi giá mới cập nhật EMA một lần.
        for (int i = period; i < span.Length; i++)
        {
            ema = Step(ema, span[i], alpha, keep);
            result[i] = ema;
        }

        return result;
    }

    /// <summary>
    /// Chỉ hai giá trị EMA cuối (nến trước và nến cuối), không cấp phát mảng.
    /// Cùng thứ tự phép tính với <see cref="Calculate"/> nên cho kết quả giống hệt.
    /// Cần ít nhất <paramref name="period"/> + 1 giá trị.
    /// </summary>
    /// <remarks>Chiến lược chỉ cần 2 giá trị này để biết hai đường có vừa cắt nhau không.</remarks>
    public static (decimal Prev, decimal Last) LastTwo(ReadOnlySpan<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        if (values.Length < period + 1)
            throw new ArgumentException($"Cần ít nhất {period + 1} giá trị, có {values.Length}.", nameof(values));

        decimal ema = Seed(values, period);
        decimal prev = ema;
        (decimal alpha, decimal keep) = Weights(period);
        for (int i = period; i < values.Length; i++)
        {
            // Giữ lại giá trị trước khi cập nhật: khi vòng lặp kết thúc, prev là EMA của nến trước.
            prev = ema;
            ema = Step(ema, values[i], alpha, keep);
        }

        return (prev, ema);
    }

    /// <summary>Giá trị EMA đầu tiên: trung bình cộng của <paramref name="period"/> giá đầu tiên.</summary>
    private static decimal Seed(ReadOnlySpan<decimal> values, int period)
    {
        decimal sum = 0m;
        for (int i = 0; i < period; i++)
            sum += values[i];
        return sum / period;
    }

    /// <summary>alpha = 2 / (period + 1); keep = 1 − alpha (tính một lần thay vì mỗi vòng lặp).</summary>
    private static (decimal Alpha, decimal Keep) Weights(int period)
    {
        decimal alpha = 2m / (period + 1);
        return (alpha, 1 - alpha);
    }

    /// <summary>Một bước cập nhật: trộn giá mới (tỷ lệ alpha) với EMA cũ (tỷ lệ keep).</summary>
    private static decimal Step(decimal ema, decimal value, decimal alpha, decimal keep) =>
        alpha * value + keep * ema;
}
