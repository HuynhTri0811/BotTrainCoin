using System.Globalization;

namespace CryptoBot.Application.MarketData;

/// <summary>Một sự kiện thị trường trong tháng, đọc từ marketevents.json (section "MarketEvents").</summary>
public sealed class MarketEvent
{
    /// <summary>Tháng xảy ra, dạng "yyyy-MM".</summary>
    public string Month { get; set; } = "";

    /// <summary>Symbol liên quan (ví dụ "ETCUSD"); để trống là sự kiện chung của cả thị trường.</summary>
    public List<string> Symbols { get; set; } = [];

    public string Title { get; set; } = "";
    public string? Detail { get; set; }
}

public sealed record MonthEvent(int Year, int Month, string Title, string? Detail, bool MarketWide);

/// <summary>
/// Danh sách sự kiện ghi tay để đối chiếu với các tháng giá tăng hoặc giảm mạnh. Chỉ là sự kiện cùng tháng,
/// không khẳng định đó là nguyên nhân.
/// </summary>
public sealed class MarketEventCatalog
{
    private readonly List<(int Year, int Month, MarketEvent Event)> _events;

    public MarketEventCatalog(IEnumerable<MarketEvent> events)
    {
        _events = events.Select(marketEvent =>
        {
            if (!DateTime.TryParseExact(marketEvent.Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var month))
                throw new InvalidOperationException($"MarketEvents: Month '{marketEvent.Month}' phải có dạng yyyy-MM ({marketEvent.Title}).");
            if (string.IsNullOrWhiteSpace(marketEvent.Title))
                throw new InvalidOperationException($"MarketEvents: sự kiện tháng {marketEvent.Month} thiếu Title.");
            return (month.Year, month.Month, marketEvent);
        }).ToList();
    }

    /// <summary>Sự kiện chung và sự kiện riêng của <paramref name="symbol"/>, tăng dần theo tháng.</summary>
    public IReadOnlyList<MonthEvent> For(string symbol) =>
        _events
            .Where(entry => entry.Event.Symbols.Count == 0 ||
                        entry.Event.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Year).ThenBy(entry => entry.Month)
            .Select(entry => new MonthEvent(entry.Year, entry.Month, entry.Event.Title, entry.Event.Detail,
                entry.Event.Symbols.Count == 0))
            .ToList();
}
