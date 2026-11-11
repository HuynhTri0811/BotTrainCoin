namespace CryptoBot.Application.MarketData;

/// <summary>Kết quả một lần tải nến lịch sử.</summary>
/// <param name="Inserted">Số nến mới được lưu.</param>
/// <param name="Requests">Số request đã gửi tới sàn.</param>
/// <param name="RejectedCandles">Số nến bị loại vì sai thứ tự/giá trị (không lưu, có ghi log).</param>
/// <param name="FirstOpenTimeUtc">Nến sớm nhất có trong DB sau khi tải; null nếu vẫn chưa có nến nào.</param>
/// <param name="ReachedRequestLimit">Dừng vì chạm giới hạn số request; gọi lại để tải tiếp.</param>
public sealed record CandleBackfillResult(
    int Inserted,
    int Requests,
    int RejectedCandles,
    DateTime? FirstOpenTimeUtc,
    bool ReachedRequestLimit);
