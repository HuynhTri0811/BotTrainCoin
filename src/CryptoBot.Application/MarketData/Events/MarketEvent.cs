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
