namespace CryptoBot.Api.Endpoints;

/// <summary>Gợi ý cho form backtest: chỉ báo tự viết dùng được làm tín hiệu và mặc định của Futures.</summary>
public sealed record BacktestOptionsDto(IReadOnlyList<BacktestIndicatorChoiceDto> Indicators,
    BacktestFuturesDefaultsDto Futures);

/// <summary>PresetName: chỉ báo đang cấu hình cùng cặp/khung nến đã lấy tham số; null = mặc định của class.</summary>
public sealed record BacktestIndicatorChoiceDto(string Type, string Description,
    IReadOnlyDictionary<string, string> Parameters, string? PresetName);

/// <summary>
/// Mặc định Futures theo %: phí/trượt giá/ký quỹ của Paper Futures, margin/đòn bẩy/SL/TP của quy tắc tự động.
/// </summary>
public sealed record BacktestFuturesDefaultsDto(decimal FeePercent, decimal SlippagePercent,
    decimal MaintenanceMarginPercent, int MaxLeverage, decimal Margin, int Leverage, decimal StopLossPercent,
    decimal TakeProfitPercent);
