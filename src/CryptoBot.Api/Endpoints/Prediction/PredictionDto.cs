namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Dự đoán nến mở lúc NextCandleTime đóng trên (Up) hay dưới (Down) LastClose,
/// kèm tỷ lệ đúng khi chạy thử trên các nến đã đóng đang xem.
/// </summary>
public sealed record PredictionDto(string Symbol, string Interval, long NextCandleTime, decimal LastClose,
    string Direction, decimal Score, string Reason, int BacktestPredictions, int BacktestCorrect,
    decimal HitRatePercent, IReadOnlyList<PredictionHistoryDto> Recent, PredictionSettingsDto Settings);
