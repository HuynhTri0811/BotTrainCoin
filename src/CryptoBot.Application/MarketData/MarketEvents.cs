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
        _events = events.Select(e =>
        {
            if (!DateTime.TryParseExact(e.Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var month))
                throw new InvalidOperationException($"MarketEvents: Month '{e.Month}' phải có dạng yyyy-MM ({e.Title}).");
            if (string.IsNullOrWhiteSpace(e.Title))
                throw new InvalidOperationException($"MarketEvents: sự kiện tháng {e.Month} thiếu Title.");
            return (month.Year, month.Month, e);
        }).ToList();
    }

    /// <summary>Sự kiện chung và sự kiện riêng của <paramref name="symbol"/>, tăng dần theo tháng.</summary>
    public IReadOnlyList<MonthEvent> For(string symbol) =>
        _events
            .Where(x => x.Event.Symbols.Count == 0 ||
                        x.Event.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new MonthEvent(x.Year, x.Month, x.Event.Title, x.Event.Detail, x.Event.Symbols.Count == 0))
            .ToList();
}
