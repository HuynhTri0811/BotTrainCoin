namespace CryptoBot.Domain.Strategies;

public static class Ema
{
    /// <summary>
    /// Tính EMA cho chuỗi giá trị. Seed là SMA của <paramref name="period"/> phần tử đầu;
    /// các vị trí trước seed trả về null. Cùng một cách khởi tạo cho live, paper và backtest.
    /// </summary>
    public static decimal?[] Calculate(IReadOnlyList<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        var result = new decimal?[values.Count];
        if (values.Count < period)
            return result;

        ReadOnlySpan<decimal> span = values as decimal[] ?? values.ToArray();
        var ema = Seed(span, period);
        result[period - 1] = ema;

        var (alpha, keep) = Weights(period);
        for (var i = period; i < span.Length; i++)
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
    public static (decimal Prev, decimal Last) LastTwo(ReadOnlySpan<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        if (values.Length < period + 1)
            throw new ArgumentException($"Cần ít nhất {period + 1} giá trị, có {values.Length}.", nameof(values));

        var ema = Seed(values, period);
        var prev = ema;
        var (alpha, keep) = Weights(period);
        for (var i = period; i < values.Length; i++)
        {
            prev = ema;
            ema = Step(ema, values[i], alpha, keep);
        }

        return (prev, ema);
    }

    private static decimal Seed(ReadOnlySpan<decimal> values, int period)
    {
        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += values[i];
        return sum / period;
    }

    /// <summary>alpha = 2 / (period + 1); keep = 1 − alpha (tính một lần thay vì mỗi vòng lặp).</summary>
    private static (decimal Alpha, decimal Keep) Weights(int period)
    {
        var alpha = 2m / (period + 1);
        return (alpha, 1 - alpha);
    }

    private static decimal Step(decimal ema, decimal value, decimal alpha, decimal keep) =>
        alpha * value + keep * ema;
}
