namespace CryptoBot.Application.Indicators;

/// <summary>Một mục trong <c>appsettings.json</c> → <c>Indicators</c>.</summary>
public sealed class IndicatorDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Exchange { get; set; } = "Binance";
    public string Symbol { get; set; } = "";
    public string Interval { get; set; } = "15m";
    public bool Enabled { get; set; } = true;

    /// <summary>Tên property của chỉ báo → giá trị (chuỗi, số dùng dấu chấm thập phân).</summary>
    public Dictionary<string, string?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
