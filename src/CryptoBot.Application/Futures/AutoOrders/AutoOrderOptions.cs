namespace CryptoBot.Application.Futures;

public sealed class AutoOrderOptions
{
    public decimal DefaultMargin { get; set; } = 50m;
    public int DefaultLeverage { get; set; } = 2;
    public decimal DefaultStopLossPercent { get; set; } = 1m;
    public decimal DefaultTakeProfitPercent { get; set; } = 2m;

    /// <summary>Chặn lệnh tự động mới khi lỗ trong ngày UTC vượt mức này so với vốn đầu ngày (%).</summary>
    public decimal DailyLossLimitPercent { get; set; } = 5m;

    public int MaxActiveRules { get; set; } = 10;
}
