namespace CryptoBot.Api.Endpoints;

/// <summary>AverageMovePercent: trung bình % giá đi theo hướng tín hiệu (âm = đi ngược).</summary>
public sealed record HorizonAccuracyDto(int Candles, int Signals, int Correct, decimal HitRatePercent,
    decimal AverageMovePercent);
