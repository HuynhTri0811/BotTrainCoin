using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.LivePrices;

/// <param name="AsOfDayUtc">Ngày (nến đã đóng) cuối cùng dùng để dự báo.</param>
/// <param name="AnchorPrice">Giá đóng cửa của ngày đó trên sàn của bảng giá (Binance); khoảng giá tính từ đây.</param>
/// <param name="TargetCloseUtc">Thời điểm đóng cửa được dự báo (cuối ngày hôm nay với 24 giờ).</param>
/// <param name="Low68">Giá: AnchorPrice × (1 + Low68Percent / 100).</param>
public sealed record SimilarDaysResult(
    string HistorySymbol,
    string Exchange,
    DateTime AsOfDayUtc,
    decimal AnchorPrice,
    DateTime TargetCloseUtc,
    decimal Low68,
    decimal High68,
    SimilarDaysForecast Forecast,
    SimilarDaysBacktest Backtest);
