namespace CryptoBot.Api.Endpoints;

/// <summary>Sự kiện cùng tháng (marketevents.json), không khẳng định là nguyên nhân.</summary>
public sealed record MonthEventDto(int Year, int Month, string Title, string? Detail, bool MarketWide);
