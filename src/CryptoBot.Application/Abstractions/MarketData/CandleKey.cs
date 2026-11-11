using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Abstractions;

public sealed record CandleKey(string Exchange, string Symbol, CandleInterval Interval)
{
    /// <summary>Khóa nến (sàn, cặp, khung) mà instance đọc. Mọi nơi tạo khóa nến từ instance đều đi qua đây.</summary>
    /// <exception cref="ArgumentException">Mã khung nến không hợp lệ.</exception>
    public static CandleKey For(StrategyInstance instance) =>
        new(instance.Exchange, instance.Symbol, CandleInterval.Parse(instance.Interval));

    public override string ToString() => $"{Exchange}:{Symbol}:{Interval.Code}";
}
