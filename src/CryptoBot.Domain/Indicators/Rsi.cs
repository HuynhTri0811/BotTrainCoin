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

        decimal gain = 0, loss = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = values[i] - values[i - 1];
            if (change > 0) gain += change;
            else loss -= change;
        }

        var avgGain = gain / period;
        var avgLoss = loss / period;
        result[period] = ToRsi(avgGain, avgLoss);

        for (var i = period + 1; i < values.Count; i++)
        {
            var change = values[i] - values[i - 1];
            avgGain = (avgGain * (period - 1) + Math.Max(change, 0)) / period;
            avgLoss = (avgLoss * (period - 1) + Math.Max(-change, 0)) / period;
            result[i] = ToRsi(avgGain, avgLoss);
        }

        return result;
    }

    private static decimal ToRsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0)
            return avgGain == 0 ? 50m : 100m;
        return 100m - 100m / (1 + avgGain / avgLoss);
    }
}
