using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>Tham số EMA muốn thử thay cho tham số của instance; trường null giữ giá trị của instance.</summary>
public sealed record EmaCrossOverrides(int? FastPeriod, int? SlowPeriod, int? LookbackCandles)
{
    /// <summary>Ghép với tham số của instance.</summary>
    public EmaCrossParameters ApplyTo(EmaCrossParameters defaults) => new(FastPeriod ?? defaults.FastPeriod,
        SlowPeriod ?? defaults.SlowPeriod, LookbackCandles ?? defaults.LookbackCandles);
}
