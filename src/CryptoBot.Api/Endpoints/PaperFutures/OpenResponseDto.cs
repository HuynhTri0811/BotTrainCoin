namespace CryptoBot.Api.Endpoints;

public sealed record OpenResponseDto(bool Accepted, bool Duplicate, IReadOnlyList<string> Errors, PositionDto? Position);
