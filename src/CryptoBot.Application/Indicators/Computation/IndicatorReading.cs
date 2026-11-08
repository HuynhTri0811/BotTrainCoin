using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Indicators;

/// <summary>Giá trị chỉ báo tại nến đã đóng mới nhất.</summary>
public sealed record IndicatorReading(ConfiguredIndicator Indicator, Candle Candle, IndicatorResult Result);
