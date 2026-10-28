using CryptoBot.Domain.Common;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Prediction;

/// <summary>Một tín hiệu lên/xuống tại nến <paramref name="Index"/> (đã đóng) trong chuỗi nến.</summary>
public sealed record DirectionSignal(int Index, PriceDirection Direction);

/// <summary>
/// Kết quả các tín hiệu sau <paramref name="Candles"/> nến: đúng khi giá đóng lúc đó đi đúng hướng so với
/// giá đóng của nến phát tín hiệu.
/// </summary>
/// <param name="Candles">Số nến sau tín hiệu.</param>
/// <param name="Signals">Số tín hiệu đã có đủ nến phía sau để chấm (giá không đổi thì bỏ qua).</param>
/// <param name="Correct">Số tín hiệu đi đúng hướng.</param>
/// <param name="AverageMovePercent">
/// Trung bình % giá đi theo hướng tín hiệu: dương là trung bình đi đúng hướng, âm là đi ngược.
/// </param>
public sealed record HorizonAccuracy(int Candles, int Signals, int Correct, decimal AverageMovePercent)
{
    /// <summary>Tỷ lệ đúng (%); 0 khi chưa có tín hiệu nào được chấm.</summary>
    public decimal HitRatePercent => Signals == 0 ? 0 : (decimal)Correct / Signals * 100;
}

/// <summary>Chấm điểm tín hiệu lên/xuống của bất kỳ chỉ báo nào trên lịch sử nến.</summary>
public static class SignalAccuracy
{
    /// <summary>Các mốc mặc định: sau 1, 3, 6 và 12 nến.</summary>
    public static readonly IReadOnlyList<int> DefaultHorizons = [1, 3, 6, 12];

    /// <summary>
    /// Với mỗi mốc trong <paramref name="horizons"/>, đếm số tín hiệu mà giá sau ngần ấy nến đi đúng hướng.
    /// Tín hiệu quá gần cuối chuỗi (chưa đủ nến phía sau) không được tính ở mốc đó.
    /// </summary>
    public static IReadOnlyList<HorizonAccuracy> Evaluate(IReadOnlyList<Candle> candles,
        IReadOnlyList<DirectionSignal> signals, IReadOnlyList<int> horizons)
    {
        if (horizons.Any(h => h <= 0))
            throw new ArgumentOutOfRangeException(nameof(horizons), "Số nến phải lớn hơn 0.");
        if (signals.Any(s => s.Index < 0 || s.Index >= candles.Count))
            throw new ArgumentOutOfRangeException(nameof(signals), "Vị trí tín hiệu nằm ngoài chuỗi nến.");

        List<HorizonAccuracy> result = new(horizons.Count);
        foreach (int horizon in horizons)
        {
            int counted = 0;
            int correct = 0;
            decimal totalMove = 0;
            foreach (DirectionSignal signal in signals)
            {
                if (MovePercent(candles, signal, horizon) is not { } move || move == 0)
                    continue;

                counted++;
                totalMove += move;
                if (move > 0)
                    correct++;
            }

            result.Add(new HorizonAccuracy(horizon, counted, correct, counted == 0 ? 0 : totalMove / counted));
        }

        return result;
    }

    /// <summary>
    /// % giá đi theo hướng tín hiệu sau <paramref name="horizon"/> nến (dương = đúng hướng);
    /// null khi chưa đủ nến phía sau.
    /// </summary>
    public static decimal? MovePercent(IReadOnlyList<Candle> candles, DirectionSignal signal, int horizon)
    {
        int target = signal.Index + horizon;
        if (target >= candles.Count)
            return null;

        decimal change = PercentChange.Between(candles[signal.Index].Close, candles[target].Close);
        return signal.Direction == PriceDirection.Up ? change : -change;
    }
}
