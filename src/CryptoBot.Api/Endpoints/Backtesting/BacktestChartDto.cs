namespace CryptoBot.Api.Endpoints;

/// <summary>Chart kết quả: mỗi cột gộp <see cref="CandlesPerBar"/> nến liên tiếp.</summary>
public sealed record BacktestChartDto(int CandlesPerBar, IReadOnlyList<BacktestBarDto> Bars,
    IReadOnlyList<BacktestMarkerDto> Markers);
