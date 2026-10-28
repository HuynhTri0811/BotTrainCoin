using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.MarketData;

/// <summary>Hướng nghiêng của dự báo "ngày tương tự".</summary>
public enum ForecastLean
{
    /// <summary>Xác suất tăng trong khoảng (40%, 60%): gần như tung đồng xu.</summary>
    Neutral,
    Up,
    Down,
}

/// <summary>
/// Dự báo bằng "những ngày tương tự hôm nay": mô tả mỗi ngày bằng 5 con số (khoảng cách giá tới EMA200 và EMA50,
/// RSI14, % thay đổi 7 ngày, % thay đổi 1 ngày), tìm <see cref="Neighbors"/> ngày trong lịch sử giống hôm nay nhất,
/// rồi xem sau <see cref="HorizonDays"/> ngày giá của chúng đã đi thế nào.
/// Khác <see cref="DailyMoveRange"/> (luôn đối xứng), khoảng ở đây có thể lệch về phía hay xảy ra hơn.
/// Kiểm chứng trên BTC/ETH từ 2016: với 24 giờ chỉ đúng khoảng 54–55% khi nghiêng mạnh; với 7 ngày không có lợi thế.
/// </summary>
/// <param name="UpPercent">Tỷ lệ ngày tương tự có giá sau đó cao hơn (%).</param>
/// <param name="MedianPercent">Trung vị % thay đổi sau <see cref="HorizonDays"/> ngày.</param>
/// <param name="Low68Percent">Phân vị 16% của % thay đổi (biên dưới khoảng ~68%).</param>
/// <param name="High68Percent">Phân vị 84% của % thay đổi (biên trên khoảng ~68%).</param>
public sealed record SimilarDaysForecast(
    int HorizonDays,
    int Neighbors,
    decimal UpPercent,
    decimal MedianPercent,
    decimal Low68Percent,
    decimal High68Percent,
    ForecastLean Lean)
{
    public const int DefaultNeighbors = 60;

    /// <summary>Từ mức này trở lên (hoặc 100 trừ mức này trở xuống) mới coi là nghiêng rõ.</summary>
    public const decimal StrongLeanPercent = 60m;

    /// <summary>Ngày đầu tiên có đủ 5 đặc trưng (EMA200 cần 200 giá).</summary>
    private const int FirstFeatureIndex = 200;

    /// <summary>
    /// Dự báo cho ngày cuối của <paramref name="dailyCloses"/> (giá đóng cửa nến ngày đã đóng, tăng dần).
    /// Null khi lịch sử chưa đủ <paramref name="neighbors"/> ngày đã biết kết quả.
    /// </summary>
    public static SimilarDaysForecast? Compute(IReadOnlyList<decimal> dailyCloses, int horizonDays = 1,
        int neighbors = DefaultNeighbors)
    {
        Validate(dailyCloses, horizonDays, neighbors);
        if (dailyCloses.Count <= FirstFeatureIndex)
            return null;

        Features features = Features.Build(dailyCloses);
        int today = dailyCloses.Count - 1;
        List<decimal>? outcomes = NeighborOutcomes(dailyCloses, features, today, today - horizonDays, horizonDays,
            neighbors);
        return outcomes is null ? null : Summarize(outcomes, horizonDays);
    }

    /// <summary>
    /// Chạy thử walk-forward: với mỗi ngày từ sau <paramref name="warmupDays"/> ngày đầu, chỉ dùng các ngày đã biết
    /// kết quả trước đó để đoán, rồi so với thực tế. Độ lệch chuẩn để chuẩn hoá đặc trưng tính trên cả chuỗi
    /// (chỉ đổi thang đo, không dùng kết quả tương lai). Bỏ qua ngày hòa (đúng 50%) và ngày giá đứng yên,
    /// cùng quy tắc với <see cref="UpPercent"/> (chỉ đếm ngày tăng thật).
    /// </summary>
    public static SimilarDaysBacktest Backtest(IReadOnlyList<decimal> dailyCloses, int horizonDays = 1,
        int neighbors = DefaultNeighbors, int warmupDays = 730)
    {
        Validate(dailyCloses, horizonDays, neighbors);
        if (dailyCloses.Count <= FirstFeatureIndex)
            return new SimilarDaysBacktest(0, 0, 0, 0);

        Features features = Features.Build(dailyCloses);
        int days = 0, correct = 0, strongDays = 0, strongCorrect = 0;
        for (int i = FirstFeatureIndex + warmupDays; i + horizonDays < dailyCloses.Count; i++)
        {
            List<decimal>? outcomes = NeighborOutcomes(dailyCloses, features, i, i - horizonDays, horizonDays,
                neighbors);
            if (outcomes is null)
                continue;

            // Hòa (đúng 50% ngày tương tự tăng) là không đoán; giá đứng yên thì không có hướng để chấm.
            decimal up = UpShare(outcomes);
            if (up == 50m || dailyCloses[i + horizonDays] == dailyCloses[i])
                continue;

            bool hit = up > 50m == dailyCloses[i + horizonDays] > dailyCloses[i];
            days++;
            if (hit) correct++;
            if (up >= StrongLeanPercent || up <= 100m - StrongLeanPercent)
            {
                strongDays++;
                if (hit) strongCorrect++;
            }
        }

        return new SimilarDaysBacktest(days, correct, strongDays, strongCorrect);
    }

    private static void Validate(IReadOnlyList<decimal> dailyCloses, int horizonDays, int neighbors)
    {
        DailyCloses.Validate(dailyCloses, horizonDays);
        if (neighbors <= 0)
            throw new ArgumentOutOfRangeException(nameof(neighbors), "Số ngày tương tự phải lớn hơn 0.");
    }

    /// <summary>% thay đổi sau horizon ngày của <paramref name="neighbors"/> ngày (≤ lastCandidate) giống ngày target nhất.</summary>
    private static List<decimal>? NeighborOutcomes(IReadOnlyList<decimal> closes, Features features, int target,
        int lastCandidate, int horizonDays, int neighbors)
    {
        int count = lastCandidate - FirstFeatureIndex + 1;
        if (count < neighbors)
            return null;

        double[] distances = new double[count];
        int[] days = new int[count];
        for (int k = 0; k < count; k++)
        {
            days[k] = FirstFeatureIndex + k;
            distances[k] = features.Distance(days[k], target);
        }

        Array.Sort(distances, days);
        List<decimal> outcomes = new(neighbors);
        for (int k = 0; k < neighbors; k++)
            outcomes.Add((closes[days[k] + horizonDays] / closes[days[k]] - 1) * 100);
        return outcomes;
    }

    private static SimilarDaysForecast Summarize(List<decimal> outcomes, int horizonDays)
    {
        decimal up = UpShare(outcomes);
        outcomes.Sort();
        ForecastLean lean = up >= StrongLeanPercent ? ForecastLean.Up
            : up <= 100m - StrongLeanPercent ? ForecastLean.Down
            : ForecastLean.Neutral;
        return new SimilarDaysForecast(horizonDays, outcomes.Count, up, Percentile(outcomes, 0.5),
            Percentile(outcomes, 0.16), Percentile(outcomes, 0.84), lean);
    }

    private static decimal UpShare(List<decimal> outcomes) =>
        (decimal)outcomes.Count(o => o > 0) / outcomes.Count * 100;

    /// <summary>Phân vị đơn giản (không nội suy) trên danh sách đã sắp xếp.</summary>
    private static decimal Percentile(List<decimal> sorted, double p) => sorted[(int)(p * (sorted.Count - 1))];

    /// <summary>5 đặc trưng của mỗi ngày, đã chia cho độ lệch chuẩn để các đặc trưng nặng ngang nhau.</summary>
    private sealed class Features
    {
        private const int Count = 5;
        private readonly double[][] _rows;

        private Features(double[][] rows) => _rows = rows;

        public static Features Build(IReadOnlyList<decimal> closes)
        {
            decimal?[] ema200 = Ema.Calculate(closes, 200);
            decimal?[] ema50 = Ema.Calculate(closes, 50);
            decimal?[] rsi = Rsi.Calculate(closes, 14);

            double[][] rows = new double[closes.Count][];
            for (int i = FirstFeatureIndex; i < closes.Count; i++)
            {
                rows[i] =
                [
                    Pct(closes[i], ema200[i]!.Value),
                    Pct(closes[i], ema50[i]!.Value),
                    (double)rsi[i]!.Value,
                    Pct(closes[i], closes[i - 7]),
                    Pct(closes[i], closes[i - 1]),
                ];
            }

            for (int f = 0; f < Count; f++)
            {
                double[] column = rows.Skip(FirstFeatureIndex).Select(r => r[f]).ToArray();
                double mean = column.Average();
                double sd = Math.Sqrt(column.Sum(v => (v - mean) * (v - mean)) / column.Length);
                if (sd == 0)
                    sd = 1;
                for (int i = FirstFeatureIndex; i < rows.Length; i++)
                    rows[i][f] /= sd;
            }

            return new Features(rows);
        }

        public double Distance(int a, int b)
        {
            double sum = 0;
            for (int f = 0; f < Count; f++)
            {
                double d = _rows[a][f] - _rows[b][f];
                sum += d * d;
            }

            return sum;
        }

        private static double Pct(decimal value, decimal reference) => (double)((value / reference - 1) * 100);
    }
}

/// <summary>Kết quả chạy thử walk-forward của <see cref="SimilarDaysForecast"/>.</summary>
/// <param name="StrongDays">Số ngày dự báo nghiêng rõ (xác suất tăng ≥ 60% hoặc ≤ 40%).</param>
public sealed record SimilarDaysBacktest(int Days, int Correct, int StrongDays, int StrongCorrect)
{
    public decimal HitRatePercent => Days == 0 ? 0 : (decimal)Correct / Days * 100;
    public decimal StrongHitRatePercent => StrongDays == 0 ? 0 : (decimal)StrongCorrect / StrongDays * 100;
}
