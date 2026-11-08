namespace CryptoBot.Domain.Prediction;

/// <summary>Một tín hiệu lên/xuống tại nến <paramref name="Index"/> (đã đóng) trong chuỗi nến.</summary>
public sealed record DirectionSignal(int Index, PriceDirection Direction);
