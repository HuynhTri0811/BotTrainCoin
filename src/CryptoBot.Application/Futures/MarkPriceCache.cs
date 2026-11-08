using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;

namespace CryptoBot.Application.Futures;

/// <summary>Mark price mới nhất trong bộ nhớ. Không dùng giá cũ hơn MarkPriceMaxAge để khớp lệnh.</summary>
public sealed class MarkPriceCache(IClock clock, PaperFuturesOptions options)
{
    private readonly ConcurrentDictionary<string, MarkPrice> _prices = new();

    public void Set(MarkPrice price) => _prices[price.Symbol] = price;

    public MarkPrice? Get(string symbol) => _prices.GetValueOrDefault(symbol);

    public MarkPrice? GetFresh(string symbol) =>
        Get(symbol) is { } markPrice && clock.UtcNow - markPrice.TimeUtc <= options.MarkPriceMaxAge ? markPrice : null;
}
