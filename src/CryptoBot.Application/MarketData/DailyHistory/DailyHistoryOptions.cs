namespace CryptoBot.Application.MarketData;

public sealed class DailyHistoryOptions
{
    /// <summary>Tải nến từ ngày này khi DB chưa có dữ liệu của symbol.</summary>
    public DateOnly StartDate { get; set; } = new(2016, 1, 1);

    /// <summary>Nến ngày chỉ đổi một lần mỗi ngày nên không cần poll dày.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Số nến mỗi request REST (Bitfinex tối đa 10000).</summary>
    public int BatchSize { get; set; } = 10000;

    /// <summary>Symbol theo quy ước của sàn, ví dụ "BTCUSD". Lấy hoàn toàn từ appsettings.json.</summary>
    public List<string> Symbols { get; set; } = [];
}
