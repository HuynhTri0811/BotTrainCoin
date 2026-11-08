namespace CryptoBot.Api.Endpoints;

public sealed record YearChangeDto(int Year, decimal Open, decimal Close, decimal ChangePercent, bool Complete);
