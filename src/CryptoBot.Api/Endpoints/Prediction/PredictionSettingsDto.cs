namespace CryptoBot.Api.Endpoints;

public sealed record PredictionSettingsDto(int TrendPeriod, int MomentumPeriod, decimal TrendWeight,
    decimal MomentumWeight, decimal PressureWeight);
