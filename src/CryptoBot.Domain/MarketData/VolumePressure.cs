namespace CryptoBot.Domain.MarketData;

/// <summary>Phía nào đang áp đảo trong một nến.</summary>
public enum VolumePressureSide
{
    /// <summary>Chênh lệch mua/bán chưa tới ngưỡng cảnh báo.</summary>
    Balanced,

    /// <summary>Mua chủ động lớn hơn bán chủ động từ ngưỡng trở lên.</summary>
    BuyHeavy,

    /// <summary>Bán chủ động lớn hơn mua chủ động từ ngưỡng trở lên.</summary>
    SellHeavy,
}

/// <summary>
/// So sánh khối lượng mua và bán chủ động (taker) của một nến.
/// Mua chủ động = lệnh mua khớp ngay vào giá bán đang chờ (đẩy giá lên); bán chủ động là phần còn lại của Volume.
/// Cảnh báo khi phía lớn hơn vượt phía nhỏ hơn ít nhất <c>alertExcessPercent</c> %, ví dụ 80%:
/// bán ≥ mua × 1,8 (bán chiếm ≥ ~64% tổng khối lượng) → <see cref="VolumePressureSide.SellHeavy"/>, và ngược lại.
/// </summary>
/// <param name="BuyVolume">Khối lượng mua chủ động (coin).</param>
/// <param name="SellVolume">Khối lượng bán chủ động (coin).</param>
/// <param name="BuyPercent">Tỷ trọng mua trong tổng khối lượng (0–100).</param>
/// <param name="ExcessPercent">Phía lớn hơn vượt phía nhỏ hơn bao nhiêu %; null khi phía nhỏ hơn bằng 0.</param>
/// <param name="Side">Kết quả so với ngưỡng cảnh báo.</param>
public sealed record VolumePressure(
    decimal BuyVolume,
    decimal SellVolume,
    decimal BuyPercent,
    decimal? ExcessPercent,
    VolumePressureSide Side)
{
    /// <summary>Ngưỡng mặc định: một phía lớn hơn phía kia 80%.</summary>
    public const decimal DefaultAlertExcessPercent = 80m;

    /// <summary>True khi một phía áp đảo, cần cảnh báo.</summary>
    public bool IsAlert => Side != VolumePressureSide.Balanced;

    /// <summary>Phân tích một nến; null khi nến chưa có khối lượng mua chủ động.</summary>
    public static VolumePressure? From(Candle candle, decimal alertExcessPercent = DefaultAlertExcessPercent) =>
        candle.TakerBuyVolume is { } takerBuy ? Analyze(candle.Volume, takerBuy, alertExcessPercent) : null;

    /// <summary>
    /// Tách <paramref name="volume"/> thành mua/bán chủ động rồi so với ngưỡng.
    /// Nến không có giao dịch (volume = 0) luôn là <see cref="VolumePressureSide.Balanced"/>.
    /// </summary>
    public static VolumePressure Analyze(decimal volume, decimal takerBuyVolume, decimal alertExcessPercent)
    {
        if (alertExcessPercent <= 0)
            throw new ArgumentOutOfRangeException(nameof(alertExcessPercent), "Ngưỡng cảnh báo phải lớn hơn 0%.");
        if (volume < 0 || takerBuyVolume < 0 || takerBuyVolume > volume)
            throw new ArgumentException("Khối lượng mua chủ động phải nằm trong [0, Volume].", nameof(takerBuyVolume));

        decimal buy = takerBuyVolume;
        decimal sell = volume - takerBuyVolume;
        if (volume == 0)
            return new VolumePressure(0, 0, 50, 0, VolumePressureSide.Balanced);

        decimal buyPercent = buy / volume * 100;
        decimal larger = Math.Max(buy, sell);
        decimal smaller = Math.Min(buy, sell);
        // Phía nhỏ hơn bằng 0: toàn bộ khối lượng nghiêng về một phía, chênh lệch vô hạn.
        decimal? excess = smaller == 0 ? null : (larger / smaller - 1) * 100;
        bool alert = excess is null || excess >= alertExcessPercent;

        VolumePressureSide side = !alert ? VolumePressureSide.Balanced
            : sell > buy ? VolumePressureSide.SellHeavy
            : VolumePressureSide.BuyHeavy;
        return new VolumePressure(buy, sell, buyPercent, excess, side);
    }
}
