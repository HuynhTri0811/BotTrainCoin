namespace CryptoBot.Application.Backtesting;

/// <summary>Giá trị gợi ý cho form backtest của một strategy instance.</summary>
/// <param name="Indicators">Các loại chỉ báo tự viết dùng được làm tín hiệu Futures.</param>
/// <param name="Futures">Mặc định của backtest Futures, lấy theo Paper Futures và quy tắc tự động.</param>
public sealed record BacktestOptions(IReadOnlyList<BacktestIndicatorChoice> Indicators,
    BacktestFuturesDefaults Futures);

/// <summary>Một loại chỉ báo và tham số gợi ý.</summary>
/// <param name="Type">Loại chỉ báo, ví dụ "MyRsi".</param>
/// <param name="Description">Mô tả ngắn của chỉ báo.</param>
/// <param name="Parameters">
/// Tham số của chỉ báo đang cấu hình cùng cặp/khung nến (<paramref name="PresetName"/>), nếu không có thì mặc định
/// của class.
/// </param>
/// <param name="PresetName">Tên chỉ báo đang cấu hình đã lấy tham số; null nếu dùng mặc định của class.</param>
public sealed record BacktestIndicatorChoice(string Type, string Description,
    IReadOnlyDictionary<string, string> Parameters, string? PresetName);

/// <summary>Mặc định của backtest Futures, theo % như trên form.</summary>
public sealed record BacktestFuturesDefaults(
    decimal FeePercent,
    decimal SlippagePercent,
    decimal MaintenanceMarginPercent,
    int MaxLeverage,
    decimal Margin,
    int Leverage,
    decimal StopLossPercent,
    decimal TakeProfitPercent);
