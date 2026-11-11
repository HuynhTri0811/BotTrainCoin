namespace CryptoBot.Api.Endpoints;

/// <summary>Nến quanh một lệnh backtest; mỗi cột gộp <see cref="CandlesPerBar"/> nến (thường là 1).</summary>
public sealed record BacktestTradeChartDto(int CandlesPerBar, IReadOnlyList<BacktestBarDto> Bars);
