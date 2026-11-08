namespace CryptoBot.Domain.Strategies;

/// <summary>
/// Tham số của chiến lược EMA cross (đọc từ appsettings.json → "Strategies" → "Parameters").
/// </summary>
/// <param name="FastPeriod">Số nến của EMA nhanh (mặc định 9): bám giá sát, đổi hướng sớm.</param>
/// <param name="SlowPeriod">Số nến của EMA chậm (mặc định 21): mượt hơn, thể hiện xu hướng chung.</param>
/// <param name="LookbackCandles">Số nến dùng để tính mỗi lần (mặc định 300), xem giải thích ở class bên dưới.</param>
public sealed record EmaCrossParameters(int FastPeriod = 9, int SlowPeriod = 21, int LookbackCandles = 300)
{
    /// <summary>Kiểm tra tham số; trả về lý do nếu sai, null nếu hợp lệ.</summary>
    public string? Validate()
    {
        if (FastPeriod < 1)
            return "FastPeriod phải >= 1.";
        // EMA nhanh phải ngắn hơn EMA chậm, nếu không thì "nhanh" và "chậm" bị đảo nghĩa.
        if (SlowPeriod <= FastPeriod)
            return "SlowPeriod phải lớn hơn FastPeriod.";
        // Cần thêm ít nhất 1 nến sau khi EMA chậm có giá trị đầu tiên, để có cả "nến trước" lẫn "nến này".
        if (LookbackCandles < SlowPeriod + 1)
            return "LookbackCandles phải >= SlowPeriod + 1.";
        return null;
    }
}
