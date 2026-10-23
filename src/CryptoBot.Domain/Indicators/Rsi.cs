namespace CryptoBot.Domain.Indicators;

/// <summary>
/// RSI (Relative Strength Index) theo cách của Wilder, giá trị 0..100.
///
/// Cách tính, nói đơn giản:
/// <list type="number">
/// <item>Với mỗi nến, lấy thay đổi giá = giá đóng cửa − giá đóng cửa nến trước.
///       Tăng thì cộng vào "lãi", giảm thì cộng (giá trị dương) vào "lỗ".</item>
/// <item>Lấy trung bình lãi và trung bình lỗ của <c>period</c> nến (mặc định 14).</item>
/// <item>RSI = 100 − 100 / (1 + trung bình lãi / trung bình lỗ).</item>
/// </list>
/// Ví dụ: trung bình lãi 2, trung bình lỗ 1 → 100 − 100/(1+2) = 66.67 (đà tăng mạnh hơn).
/// Trung bình lãi bằng trung bình lỗ → RSI = 50.
/// </summary>
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

        decimal?[] result = new decimal?[values.Count];
        // Cần period thay đổi giá, tức period + 1 giá, mới tính được RSI đầu tiên.
        if (values.Count <= period)
            return result;

        ReadOnlySpan<decimal> span = values as decimal[] ?? values.ToArray();
        Averages avg = Seed(span, period);
        // RSI đầu tiên nằm ở vị trí period (giá thứ period + 1).
        result[period] = ToRsi(avg);

        for (int i = period + 1; i < span.Length; i++)
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

        Averages avg = Seed(values, period);
        // Làm mượt lần lượt đến nến áp chót (dừng trước nến cuối)...
        for (int i = period + 1; i < values.Length - 1; i++)
            avg = Smooth(avg, values[i] - values[i - 1], period);

        // ...để lấy RSI của nến trước, rồi làm mượt thêm một lần với thay đổi giá của nến cuối.
        decimal prev = ToRsi(avg);
        avg = Smooth(avg, values[^1] - values[^2], period);
        return (prev, ToRsi(avg));
    }

    /// <summary>Trung bình lãi (Gain) và trung bình lỗ (Loss); cả hai đều là số không âm.</summary>
    private readonly record struct Averages(decimal Gain, decimal Loss);

    /// <summary>Trung bình cộng lãi/lỗ của <paramref name="period"/> thay đổi giá đầu tiên.</summary>
    private static Averages Seed(ReadOnlySpan<decimal> values, int period)
    {
        decimal gain = 0, loss = 0;
        for (int i = 1; i <= period; i++)
        {
            decimal change = values[i] - values[i - 1];
            if (change > 0) gain += change;   // nến tăng: cộng vào lãi
            else loss -= change;              // nến giảm: change âm, trừ đi để cộng phần dương vào lỗ
        }

        return new Averages(gain / period, loss / period);
    }

    /// <summary>Làm mượt kiểu Wilder với một thay đổi giá mới.</summary>
    /// <remarks>
    /// Trung bình mới = (trung bình cũ × (period − 1) + giá trị mới) / period.
    /// Với period 14: giữ 13 phần trung bình cũ, thêm 1 phần thay đổi mới, nên RSI thay đổi từ từ, không giật.
    /// </remarks>
    private static Averages Smooth(Averages avg, decimal change, int period) => new(
        (avg.Gain * (period - 1) + Math.Max(change, 0)) / period,
        (avg.Loss * (period - 1) + Math.Max(-change, 0)) / period);

    /// <summary>Đổi cặp trung bình lãi/lỗ thành RSI 0..100.</summary>
    private static decimal ToRsi(Averages avg)
    {
        // Không có nến giảm nào: không chia được cho 0, nên quy ước 100 (toàn tăng) hoặc 50 (giá đứng yên).
        if (avg.Loss == 0)
            return avg.Gain == 0 ? 50m : 100m;
        return 100m - 100m / (1 + avg.Gain / avg.Loss);
    }
}
