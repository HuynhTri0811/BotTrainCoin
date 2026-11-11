namespace CryptoBot.Api.Endpoints;

/// <summary>Lệnh còn mở ở nến cuối; lãi/lỗ tạm tính theo giá đóng cửa nến cuối, đã trừ phí vào.</summary>
public sealed record BacktestOpenTradeDto(DateTime EntryTimeUtc, decimal EntryPrice, decimal Quantity,
    decimal EntryFee, decimal MarkPrice, decimal UnrealizedPnl);
