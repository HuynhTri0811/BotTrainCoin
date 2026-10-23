namespace CryptoBot.Domain.Indicators;

public static class Rsi
{
    /// <summary>
    /// RSI theo Wilder. Giá trị đầu tiên ở vị trí <paramref name="period"/> (cần period thay đổi giá),
    /// seed bằng trung bình cộng lãi/lỗ, sau đó làm mượt kiểu Wilder: avg = (avg × (period − 1) + mới) / period.
    /// Các vị trí trước đó trả về null. Không có lỗ → 100; không lãi không lỗ (giá đứng yên) → 50.
    /// </summary>
    public static decimal?[] Calculate(IReadOnlyList<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        var result = new decimal?[values.Count];
        if (values.Count <= period)
            return result;

        ReadOnlySpan<decimal> span = values as decimal[] ?? values.ToArray();
        var avg = Seed(span, period);
        result[period] = ToRsi(avg);

        for (var i = period + 1; i < span.Length; i++)
        {
            avg = Smooth(avg, span[i] - span[i - 1], period);
            result[i] = ToRsi(avg);
        }

        return result;
    }

    /// <summary>
    /// Chỉ hai giá trị RSI cuối (nến trước và nến cuối), không cấp phát mảng.
    /// Cùng thứ tự phép tính với <see cref="Calculate"/> nên cho kết quả giống hệt.
    /// Cần ít nhất <paramref name="period"/> + 2 giá trị.
    /// </summary>
    public static (decimal Prev, decimal Last) LastTwo(ReadOnlySpan<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        if (values.Length < period + 2)
            throw new ArgumentException($"Cần ít nhất {period + 2} giá trị, có {values.Length}.", nameof(values));

        var avg = Seed(values, period);
        for (var i = period + 1; i < values.Length - 1; i++)
            avg = Smooth(avg, values[i] - values[i - 1], period);

        var prev = ToRsi(avg);
        avg = Smooth(avg, values[^1] - values[^2], period);
        return (prev, ToRsi(avg));
    }

    private readonly record struct Averages(decimal Gain, decimal Loss);

    /// <summary>Trung bình cộng lãi/lỗ của <paramref name="period"/> thay đổi giá đầu tiên.</summary>
    private static Averages Seed(ReadOnlySpan<decimal> values, int period)
    {
        decimal gain = 0, loss = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = values[i] - values[i - 1];
            if (change > 0) gain += change;
            else loss -= change;
        }

        return new Averages(gain / period, loss / period);
    }

    /// <summary>Làm mượt kiểu Wilder với một thay đổi giá mới.</summary>
    private static Averages Smooth(Averages avg, decimal change, int period) => new(
        (avg.Gain * (period - 1) + Math.Max(change, 0)) / period,
        (avg.Loss * (period - 1) + Math.Max(-change, 0)) / period);

    private static decimal ToRsi(Averages avg)
    {
        if (avg.Loss == 0)
            return avg.Gain == 0 ? 50m : 100m;
        return 100m - 100m / (1 + avg.Gain / avg.Loss);
    }
}
