namespace CryptoBot.Api.Endpoints;

/// <summary>Actual là null khi nến đóng đúng bằng giá nến trước (không tính đúng/sai).</summary>
public sealed record PredictionHistoryDto(long Time, string Predicted, decimal Score, string? Actual,
    decimal PreviousClose, decimal Close, decimal Change, decimal ChangePercent);
