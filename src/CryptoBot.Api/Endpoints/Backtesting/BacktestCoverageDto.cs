namespace CryptoBot.Api.Endpoints;

/// <summary>Khoảng nến đã đóng có trong DB (UTC); null khi chưa có nến.</summary>
public sealed record BacktestCoverageDto(DateTime? FirstOpenTimeUtc, DateTime? LastOpenTimeUtc);
