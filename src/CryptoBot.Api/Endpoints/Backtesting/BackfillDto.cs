namespace CryptoBot.Api.Endpoints;

/// <summary>Kết quả tải lịch sử. ReachedRequestLimit = true thì gọi lại để tải tiếp.</summary>
public sealed record BackfillDto(int Inserted, int Requests, int RejectedCandles, DateTime? FirstOpenTimeUtc,
    bool ReachedRequestLimit);
