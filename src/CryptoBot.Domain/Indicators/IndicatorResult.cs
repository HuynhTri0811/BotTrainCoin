namespace CryptoBot.Domain.Indicators;

/// <summary>
/// Kết quả tính chỉ báo tại nến cuối của cửa sổ (tức nến vừa đóng).
/// </summary>
/// <param name="Values">
/// Các giá trị để vẽ lên chart, khóa trùng với <see cref="IndicatorLine.Key"/>.
/// Giá trị <c>null</c> nghĩa là chưa tính được (ví dụ đang warm-up), chart sẽ bỏ trống điểm đó.
/// </param>
/// <param name="Signal">Tín hiệu phát ra tại nến này: <see cref="IndicatorSignal.Long"/>, <see cref="IndicatorSignal.Short"/> hoặc <see cref="IndicatorSignal.None"/>.</param>
/// <param name="Reason">Câu giải thích bằng chữ vì sao có (hoặc không có) tín hiệu, hiển thị trên dashboard.</param>
public sealed record IndicatorResult(
    IReadOnlyDictionary<string, decimal?> Values,
    IndicatorSignal Signal,
    string Reason)
{
    /// <summary>
    /// Tạo nhanh một kết quả <b>không có tín hiệu</b> (<see cref="IndicatorSignal.None"/>).
    /// </summary>
    /// <param name="reason">Lý do không có tín hiệu, ví dụ <c>"Warm-up: có 50/200 nến."</c> hoặc <c>"RSI 47, không cắt mức."</c>.</param>
    /// <param name="values">
    /// Các giá trị vẫn muốn vẽ lên chart dù không có tín hiệu. Truyền <c>null</c> (mặc định) khi chưa có gì để vẽ,
    /// khi đó dùng dictionary rỗng.
    /// </param>
    /// <returns>Kết quả với <see cref="Signal"/> = <see cref="IndicatorSignal.None"/>.</returns>
    public static IndicatorResult None(string reason, IReadOnlyDictionary<string, decimal?>? values = null) =>
        new(values ?? new Dictionary<string, decimal?>(), IndicatorSignal.None, reason);
}
