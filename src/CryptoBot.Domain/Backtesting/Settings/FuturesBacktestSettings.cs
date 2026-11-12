namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Thiết lập riêng của backtest Futures (USDⓈ-M, ký quỹ isolated), tính giống lệnh Paper Futures: margin mỗi lệnh lấy
/// theo <see cref="BacktestSettings.Sizing"/>/<see cref="BacktestSettings.SizeValue"/>, giá trị vị thế = margin × đòn
/// bẩy, phí <see cref="BacktestSettings.FeePercent"/> tính trên giá trị vị thế ở cả lần mở và lần đóng.
/// </summary>
/// <param name="Leverage">Đòn bẩy, 1 đến <see cref="MaxLeverage"/>.</param>
/// <param name="Direction">Được mở Long, Short hay cả hai.</param>
/// <param name="CloseOnReverse">
/// Đóng lệnh khi tín hiệu ngược chiều xuất hiện, như "Tự đóng khi đảo chiều" của quy tắc tự động. Tắt thì lệnh chỉ
/// đóng ở TP/SL hoặc khi bị thanh lý.
/// </param>
/// <param name="SlippagePercent">Trượt giá bất lợi mỗi lần khớp, % (Paper Futures mặc định 0.05).</param>
/// <param name="MaintenanceMarginPercent">Tỷ lệ ký quỹ duy trì %, để tính giá thanh lý (BTCUSDT Paper: 0.4).</param>
public sealed record FuturesBacktestSettings(
    int Leverage,
    BacktestDirection Direction,
    bool CloseOnReverse,
    decimal SlippagePercent,
    decimal MaintenanceMarginPercent)
{
    /// <summary>Đòn bẩy lớn nhất sàn cho phép.</summary>
    public const int MaxLeverage = 125;

    /// <summary>Kiểm tra cùng mức TP/SL của lần chạy; trả về lý do nếu sai, null nếu hợp lệ.</summary>
    public string? Validate(decimal? takeProfitPercent, decimal? stopLossPercent)
    {
        if (Leverage < 1 || Leverage > MaxLeverage)
            return $"Đòn bẩy phải từ 1 đến {MaxLeverage}x.";
        if (!Enum.IsDefined(Direction))
            return "Hướng lệnh không hợp lệ.";
        if (SlippagePercent is < 0 or >= 10)
            return "Trượt giá phải trong khoảng [0, 10)%.";
        if (MaintenanceMarginPercent is < 0 or >= 10)
            return "Ký quỹ duy trì phải trong khoảng [0, 10)%.";
        if (!CloseOnReverse && takeProfitPercent is null && stopLossPercent is null)
            return "Cần ít nhất một cách đóng lệnh: chốt lời, cắt lỗ hoặc đóng khi tín hiệu đảo chiều.";

        // Giống quy tắc tự động: cắt lỗ phải nằm trước giá thanh lý, nếu không lệnh bị thanh lý trước khi chạm SL.
        if (stopLossPercent is { } stopLoss)
        {
            decimal longLimit = Direction == BacktestDirection.ShortOnly ? decimal.MaxValue : LongLiquidationPercent;
            decimal shortLimit = Direction == BacktestDirection.LongOnly ? decimal.MaxValue : ShortLiquidationPercent;
            decimal limit = Math.Min(longLimit, shortLimit);
            if (stopLoss >= limit)
                return $"Cắt lỗ {stopLoss}% nằm sau giá thanh lý (cách giá vào khoảng {limit:0.##}% ở đòn bẩy " +
                       $"{Leverage}x). Giảm đòn bẩy hoặc giảm % cắt lỗ.";
        }

        return null;
    }

    /// <summary>Giá thanh lý của lệnh Long cách giá vào bao nhiêu % (không phụ thuộc giá vào).</summary>
    public decimal LongLiquidationPercent =>
        (1 - (1 - 1m / Leverage) / (1 - MaintenanceMarginPercent / 100)) * 100;

    /// <summary>Giá thanh lý của lệnh Short cách giá vào bao nhiêu %.</summary>
    public decimal ShortLiquidationPercent =>
        ((1 + 1m / Leverage) / (1 + MaintenanceMarginPercent / 100) - 1) * 100;
}
