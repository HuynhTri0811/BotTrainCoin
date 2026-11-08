using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record CloseResult(bool Ok, string? Error, FuturesPosition? Position);
