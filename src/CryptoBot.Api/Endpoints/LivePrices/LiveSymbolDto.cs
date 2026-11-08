namespace CryptoBot.Api.Endpoints;

public sealed record LiveSymbolDto(string Symbol, IReadOnlyList<LiveSampleDto> Samples);
