namespace CryptoBot.Api.Endpoints;

public sealed record NewsSyncDto(int Added, IReadOnlyList<string> Errors);
