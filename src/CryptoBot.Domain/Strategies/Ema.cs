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

        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += values[i];

        var ema = sum / period;
        result[period - 1] = ema;

        var alpha = 2m / (period + 1);
        for (var i = period; i < values.Count; i++)
        {
            ema = alpha * values[i] + (1 - alpha) * ema;
            result[i] = ema;
        }

        return result;
    }
}
