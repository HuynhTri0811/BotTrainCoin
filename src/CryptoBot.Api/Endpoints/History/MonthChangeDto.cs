namespace CryptoBot.Api.Endpoints;

public sealed record MonthChangeDto(int Year, int Month, decimal Open, decimal Close, decimal ChangePercent, int Days,
    bool Complete);
