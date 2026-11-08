using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.LivePrices;

/// <summary>Kết quả <see cref="SimilarDaysCache"/> giữ cho một (symbol, mốc) theo ngày cuối.</summary>
/// <param name="Forecast">Dự báo cho ngày cuối; null khi lịch sử chưa đủ.</param>
/// <param name="Backtest">Kết quả chạy thử walk-forward trên cùng chuỗi giá.</param>
public sealed record SimilarDaysComputation(SimilarDaysForecast? Forecast, SimilarDaysBacktest Backtest);
