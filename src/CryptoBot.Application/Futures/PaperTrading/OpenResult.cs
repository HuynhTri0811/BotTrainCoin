using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record OpenResult(OpenQuote Quote, FuturesPosition? Position, bool Duplicate);
