namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Dự đoán nến mở lúc NextCandleTime đóng trên (Up) hay dưới (Down) LastClose,
/// kèm tỷ lệ đúng khi chạy thử trên các nến đã đóng đang xem.
/// NextCandleOpenings: các sàn chứng khoán mở cửa trong nến được dự đoán; luôn rỗng với nến ngày.
/// </summary>
public sealed record PredictionDto(string Symbol, string Interval, long NextCandleTime, decimal LastClose,
    string Direction, decimal Score, string Reason, int BacktestPredictions, int BacktestCorrect,
    decimal HitRatePercent, IReadOnlyList<PredictionHistoryDto> Recent, PredictionSettingsDto Settings,
    IReadOnlyList<MarketOpeningDto> NextCandleOpenings);
