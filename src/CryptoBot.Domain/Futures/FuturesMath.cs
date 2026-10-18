namespace CryptoBot.Domain.Futures;

public enum PositionSide
{
    Long = 1,
    Short = 2,
}

/// <summary>Quy tắc giao dịch của một symbol (lấy từ exchangeInfo, cần kiểm tra lại khi tích hợp testnet).</summary>
public sealed record FuturesSymbolRules(
    string Symbol,
    decimal QuantityStep,
    decimal TickSize,
    decimal MinNotional,
    decimal MaintenanceMarginRate);

/// <summary>
/// Công thức USDⓈ-M isolated margin, mô hình đơn giản hóa cho paper trading:
/// một mức maintenance margin cố định, bỏ qua bậc rủi ro, insurance fund và funding.
/// </summary>
public static class FuturesMath
{
    public static decimal RoundDown(decimal value, decimal step) =>
        step <= 0 ? value : Math.Floor(value / step) * step;

    public static decimal RoundToTick(decimal price, decimal tick) =>
        tick <= 0 ? price : Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;

    /// <summary>Giá khớp mô phỏng: luôn bất lợi cho người đặt lệnh.</summary>
    public static decimal ApplySlippage(decimal price, PositionSide side, bool opening, decimal slippageRate)
    {
        // Mở Long / đóng Short là mua: giá cao hơn. Mở Short / đóng Long là bán: giá thấp hơn.
        var buying = (side == PositionSide.Long) == opening;
        return buying ? price * (1 + slippageRate) : price * (1 - slippageRate);
    }

    /// <summary>
    /// Giá thanh lý isolated khi margin = qty × entry / leverage:
    /// Long:  P = entry × (1 − 1/L) / (1 − mmr)
    /// Short: P = entry × (1 + 1/L) / (1 + mmr)
    /// (điểm mà margin + PnL chưa thực hiện = mmr × qty × P).
    /// </summary>
    public static decimal LiquidationPrice(PositionSide side, decimal entry, int leverage, decimal mmr)
    {
        var inv = 1m / leverage;
        return side == PositionSide.Long
            ? entry * (1 - inv) / (1 - mmr)
            : entry * (1 + inv) / (1 + mmr);
    }

    public static decimal UnrealizedPnl(PositionSide side, decimal quantity, decimal entry, decimal mark) =>
        side == PositionSide.Long ? quantity * (mark - entry) : quantity * (entry - mark);

    public static bool IsLiquidated(PositionSide side, decimal mark, decimal liquidationPrice) =>
        side == PositionSide.Long ? mark <= liquidationPrice : mark >= liquidationPrice;

    public static bool StopHit(PositionSide side, decimal mark, decimal stop) =>
        side == PositionSide.Long ? mark <= stop : mark >= stop;

    public static bool TakeProfitHit(PositionSide side, decimal mark, decimal takeProfit) =>
        side == PositionSide.Long ? mark >= takeProfit : mark <= takeProfit;
}
