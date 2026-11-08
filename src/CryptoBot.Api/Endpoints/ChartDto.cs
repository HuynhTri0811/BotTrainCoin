namespace CryptoBot.Api.Endpoints;

public sealed record ChartDto(StrategyDto Strategy, IReadOnlyList<ChartPointDto> Candles, IReadOnlyList<SignalDto> Signals);
