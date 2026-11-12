namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Chart kết quả: mỗi cột gộp <see cref="CandlesPerBar"/> nến liên tiếp. Điểm vào/ra lấy từ <c>trades</c> và
/// <c>openTrade</c> của kết quả.
/// </summary>
public sealed record BacktestChartDto(int CandlesPerBar, IReadOnlyList<BacktestBarDto> Bars);
