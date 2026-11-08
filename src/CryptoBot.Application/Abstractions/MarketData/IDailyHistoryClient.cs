namespace CryptoBot.Application.Abstractions;

/// <summary>Nến ngày lịch sử dài hạn (từ 2016) của một sàn ngoài Binance, dùng để lưu và truy vấn.</summary>
public interface IDailyHistoryClient
{
    string Exchange { get; }

    /// <summary>Nến 1d tăng dần theo OpenTime, bắt đầu từ <paramref name="startTimeUtc"/> (bao gồm).</summary>
    Task<IReadOnlyList<KlineDto>> GetDailyKlinesAsync(string symbol, DateTime startTimeUtc, int limit,
        CancellationToken ct);
}
