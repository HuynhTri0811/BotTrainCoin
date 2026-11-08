namespace CryptoBot.Domain.Futures;

/// <summary>
/// Tập hợp các công thức tính toán cho hợp đồng tương lai USDⓈ-M, chế độ ký quỹ isolated.
/// </summary>
/// <remarks>
/// <para>
/// Đây là mô hình đơn giản hóa dùng cho paper trading (giao dịch giả lập), không phải bản sao chính xác
/// cách sàn tính toán:
/// </para>
/// <list type="bullet">
///   <item><description>Chỉ dùng <b>một</b> mức maintenance margin rate cố định cho mỗi symbol, bỏ qua các bậc rủi ro (leverage bracket).</description></item>
///   <item><description>Bỏ qua insurance fund, maintenance amount và phí funding.</description></item>
///   <item><description>Mọi giá trị tỷ lệ (slippage, mmr) đều ở dạng thập phân: <c>0.001</c> nghĩa là 0,1%.</description></item>
/// </list>
/// <para>
/// Tất cả hàm đều là hàm thuần (pure function): không có trạng thái, không gọi I/O, cùng đầu vào luôn cho cùng kết quả.
/// </para>
/// <para>
/// Quy ước chung về hướng vị thế:
/// <b>Long</b> có lãi khi giá tăng, <b>Short</b> có lãi khi giá giảm.
/// Vì vậy hầu hết các phép so sánh bên dưới đảo chiều giữa Long và Short.
/// </para>
/// </remarks>
public static class FuturesMath
{
    /// <summary>
    /// Làm tròn <b>xuống</b> một giá trị về bội số gần nhất của <paramref name="step"/>.
    /// Dùng để chuẩn hóa khối lượng đặt lệnh theo <see cref="FuturesSymbolRules.QuantityStep"/>.
    /// </summary>
    /// <param name="value">Giá trị cần làm tròn (thường là khối lượng).</param>
    /// <param name="step">Bước nhảy. Nếu <c>&lt;= 0</c> thì coi như không có ràng buộc và trả về nguyên <paramref name="value"/>.</param>
    /// <returns>Bội số lớn nhất của <paramref name="step"/> không vượt quá <paramref name="value"/>.</returns>
    /// <remarks>
    /// Làm tròn xuống (không làm tròn gần nhất) để khối lượng không bao giờ vượt quá số vốn đã tính.
    /// Ví dụ: <c>RoundDown(0.12345m, 0.001m)</c> = <c>0.123</c>.
    /// </remarks>
    public static decimal RoundDown(decimal value, decimal step) =>
        step <= 0 ? value : Math.Floor(value / step) * step;

    /// <summary>
    /// Làm tròn giá về bội số <b>gần nhất</b> của <paramref name="tick"/> (tick size của sàn).
    /// </summary>
    /// <param name="price">Giá cần làm tròn.</param>
    /// <param name="tick">Tick size. Nếu <c>&lt;= 0</c> thì trả về nguyên <paramref name="price"/>.</param>
    /// <returns>Giá đã được làm tròn theo tick; trường hợp chính giữa (.5) làm tròn ra xa số 0.</returns>
    /// <remarks>
    /// Ví dụ: <c>RoundToTick(100.05m, 0.1m)</c> = <c>100.1</c>; <c>RoundToTick(100.04m, 0.1m)</c> = <c>100.0</c>.
    /// </remarks>
    public static decimal RoundToTick(decimal price, decimal tick) =>
        tick <= 0 ? price : Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;

    /// <summary>
    /// Tính giá khớp mô phỏng sau khi áp trượt giá (slippage). Trượt giá luôn <b>bất lợi</b> cho người đặt lệnh.
    /// </summary>
    /// <param name="price">Giá tham chiếu trước khi trượt (thường là giá mark hoặc giá đóng nến).</param>
    /// <param name="side">Hướng vị thế: <see cref="PositionSide.Long"/> hoặc <see cref="PositionSide.Short"/>.</param>
    /// <param name="opening"><c>true</c> nếu là lệnh mở vị thế, <c>false</c> nếu là lệnh đóng vị thế.</param>
    /// <param name="slippageRate">Tỷ lệ trượt giá dạng thập phân, ví dụ <c>0.0005</c> = 0,05%.</param>
    /// <returns>Giá khớp sau khi trượt.</returns>
    /// <remarks>
    /// Bảng xác định chiều mua/bán:
    /// <list type="table">
    ///   <listheader><term>Trường hợp</term><description>Hành động thực tế → giá khớp</description></listheader>
    ///   <item><term>Mở Long</term><description>Mua → <c>price × (1 + slippageRate)</c> (cao hơn)</description></item>
    ///   <item><term>Đóng Short</term><description>Mua → <c>price × (1 + slippageRate)</c> (cao hơn)</description></item>
    ///   <item><term>Mở Short</term><description>Bán → <c>price × (1 − slippageRate)</c> (thấp hơn)</description></item>
    ///   <item><term>Đóng Long</term><description>Bán → <c>price × (1 − slippageRate)</c> (thấp hơn)</description></item>
    /// </list>
    /// </remarks>
    public static decimal ApplySlippage(decimal price, PositionSide side, bool opening, decimal slippageRate)
    {
        // Mở Long / đóng Short là mua: giá cao hơn. Mở Short / đóng Long là bán: giá thấp hơn.
        bool buying = (side == PositionSide.Long) == opening;
        return buying ? price * (1 + slippageRate) : price * (1 - slippageRate);
    }

    /// <summary>
    /// Tính giá thanh lý (liquidation price) của vị thế isolated, giả định margin ban đầu = <c>qty × entry / leverage</c>.
    /// </summary>
    /// <param name="side">Hướng vị thế.</param>
    /// <param name="entry">Giá vào lệnh (giá trung bình của vị thế).</param>
    /// <param name="leverage">Đòn bẩy (L), phải &gt; 0.</param>
    /// <param name="mmr">Tỷ lệ ký quỹ duy trì, dạng thập phân (xem <see cref="FuturesSymbolRules.MaintenanceMarginRate"/>).</param>
    /// <returns>Giá mark mà tại đó vị thế bị thanh lý.</returns>
    /// <remarks>
    /// <para>Công thức:</para>
    /// <list type="bullet">
    ///   <item><description>Long:  <c>P = entry × (1 − 1/L) / (1 − mmr)</c></description></item>
    ///   <item><description>Short: <c>P = entry × (1 + 1/L) / (1 + mmr)</c></description></item>
    /// </list>
    /// <para>
    /// Đây là điểm mà <c>margin + PnL chưa thực hiện = mmr × qty × P</c>.
    /// Khối lượng (qty) bị triệt tiêu ở hai vế nên kết quả không phụ thuộc vào khối lượng.
    /// </para>
    /// <para>
    /// Ví dụ: Long, entry = 100, L = 10, mmr = 0.004 → P = 100 × 0.9 / 0.996 ≈ 90,36.
    /// </para>
    /// </remarks>
    /// <exception cref="DivideByZeroException">Khi <paramref name="leverage"/> = 0.</exception>
    public static decimal LiquidationPrice(PositionSide side, decimal entry, int leverage, decimal mmr)
    {
        decimal inv = 1m / leverage;
        return side == PositionSide.Long
            ? entry * (1 - inv) / (1 - mmr)
            : entry * (1 + inv) / (1 + mmr);
    }

    /// <summary>
    /// Tính lãi/lỗ chưa thực hiện (unrealized PnL) của vị thế theo giá mark hiện tại, đơn vị USDT.
    /// </summary>
    /// <param name="side">Hướng vị thế.</param>
    /// <param name="quantity">Khối lượng vị thế (số coin, luôn dương).</param>
    /// <param name="entry">Giá vào lệnh.</param>
    /// <param name="mark">Giá mark hiện tại.</param>
    /// <returns>
    /// Số dương là lãi, số âm là lỗ.
    /// Long: <c>quantity × (mark − entry)</c>; Short: <c>quantity × (entry − mark)</c>.
    /// </returns>
    /// <remarks>Chưa trừ phí giao dịch và phí funding.</remarks>
    public static decimal UnrealizedPnl(PositionSide side, decimal quantity, decimal entry, decimal mark) =>
        side == PositionSide.Long ? quantity * (mark - entry) : quantity * (entry - mark);

    /// <summary>
    /// Kiểm tra vị thế đã chạm giá thanh lý hay chưa.
    /// </summary>
    /// <param name="side">Hướng vị thế.</param>
    /// <param name="mark">Giá mark hiện tại.</param>
    /// <param name="liquidationPrice">Giá thanh lý (tính bằng <see cref="LiquidationPrice"/>).</param>
    /// <returns>
    /// <c>true</c> nếu bị thanh lý: Long khi <c>mark &lt;= liquidationPrice</c>, Short khi <c>mark &gt;= liquidationPrice</c>.
    /// </returns>
    public static bool IsLiquidated(PositionSide side, decimal mark, decimal liquidationPrice) =>
        side == PositionSide.Long ? mark <= liquidationPrice : mark >= liquidationPrice;

    /// <summary>
    /// Kiểm tra giá mark đã chạm mức cắt lỗ (stop loss) hay chưa.
    /// </summary>
    /// <param name="side">Hướng vị thế.</param>
    /// <param name="mark">Giá mark hiện tại.</param>
    /// <param name="stop">Giá stop loss.</param>
    /// <returns>
    /// <c>true</c> nếu chạm stop: Long khi giá giảm xuống <c>mark &lt;= stop</c>, Short khi giá tăng lên <c>mark &gt;= stop</c>.
    /// </returns>
    public static bool StopHit(PositionSide side, decimal mark, decimal stop) =>
        side == PositionSide.Long ? mark <= stop : mark >= stop;

    /// <summary>
    /// Kiểm tra giá mark đã chạm mức chốt lời (take profit) hay chưa.
    /// </summary>
    /// <param name="side">Hướng vị thế.</param>
    /// <param name="mark">Giá mark hiện tại.</param>
    /// <param name="takeProfit">Giá take profit.</param>
    /// <returns>
    /// <c>true</c> nếu chạm take profit: Long khi giá tăng lên <c>mark &gt;= takeProfit</c>, Short khi giá giảm xuống <c>mark &lt;= takeProfit</c>.
    /// </returns>
    public static bool TakeProfitHit(PositionSide side, decimal mark, decimal takeProfit) =>
        side == PositionSide.Long ? mark >= takeProfit : mark <= takeProfit;
}
