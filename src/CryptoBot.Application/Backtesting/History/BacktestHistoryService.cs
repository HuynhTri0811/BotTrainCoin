using System.IO.Compression;
using System.Text.Json;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Lưu mọi lần backtest để tham khảo lại: liệt kê, mở lại đầy đủ không cần chạy lại, ghi chú, xóa.
/// Kết quả lưu dạng JSON nén gzip (một lần chạy nến 15m nhiều năm ≈ vài trăm KB sau nén).
/// </summary>
public sealed class BacktestHistoryService(IBacktestStore store, IClock clock)
{
    public const int MaxListLimit = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SavedBacktestRun> SaveAsync(BacktestRun run, CancellationToken ct)
    {
        var snapshot = BacktestSnapshot.From(run);
        var saved = new SavedBacktest(clock.UtcNow, run.Instance.Id, run.Instance.Symbol, run.Instance.Interval,
            run.Parameters.FastPeriod, run.Parameters.SlowPeriod, run.Parameters.LookbackCandles,
            run.FirstCandleUtc, run.LastCandleUtc, run.Result.Settings, run.Result.Statistics, Compress(snapshot));
        store.Add(saved);
        await store.SaveAsync(ct);
        return new SavedBacktestRun(saved, snapshot);
    }

    public Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, CancellationToken ct) =>
        store.ListAsync(strategyInstanceId, Math.Clamp(limit, 1, MaxListLimit), ct);

    public async Task<SavedBacktestRun?> GetAsync(long id, CancellationToken ct) =>
        await store.GetAsync(id, ct) is { } saved ? new SavedBacktestRun(saved, Decompress(saved.Content)) : null;

    /// <summary>Đặt ghi chú; null nếu không có lần chạy này.</summary>
    /// <exception cref="ArgumentException">Ghi chú quá dài.</exception>
    public async Task<SavedBacktest?> SetNoteAsync(long id, string? note, CancellationToken ct)
    {
        if (await store.GetAsync(id, ct) is not { } saved)
            return null;
        saved.SetNote(note);
        await store.SaveAsync(ct);
        return saved;
    }

    /// <summary>Xóa; false nếu không có lần chạy này.</summary>
    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        if (await store.GetAsync(id, ct) is not { } saved)
            return false;
        store.Remove(saved);
        await store.SaveAsync(ct);
        return true;
    }

    private static byte[] Compress(BacktestSnapshot snapshot)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal))
            JsonSerializer.Serialize(gzip, snapshot, Json);
        return buffer.ToArray();
    }

    private static BacktestSnapshot Decompress(byte[] content)
    {
        using var gzip = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<BacktestSnapshot>(gzip, Json)
               ?? throw new InvalidOperationException("Nội dung backtest đã lưu bị hỏng.");
    }
}
