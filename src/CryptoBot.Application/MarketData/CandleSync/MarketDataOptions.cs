using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.MarketData;

public sealed class MarketDataOptions
{
    /// <summary>Số nến tải lần đầu khi DB chưa có dữ liệu.</summary>
    public int BootstrapCandles { get; set; } = 1000;

    /// <summary>Số nến mỗi request REST (Binance tối đa 1000).</summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>Số batch tối đa mỗi lần sync, tránh một lần sync kéo dài vô hạn.</summary>
    public int MaxBatchesPerSync { get; set; } = 20;

    /// <summary>Số khoảng thiếu (giữa các nến đã lưu trong cửa sổ gần đây) được tải lại tối đa mỗi lần sync.</summary>
    public int MaxGapRepairsPerSync { get; set; } = 5;

    /// <summary>Số lần tải lại tối đa cho một khoảng thiếu; sàn vẫn không có nến thì bỏ, không tải lại mãi.</summary>
    public int MaxGapRepairAttempts { get; set; } = 12;

    /// <summary>Khoảng cách giữa hai lần tải lại cùng một khoảng thiếu.</summary>
    public TimeSpan GapRepairRetryInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Không sync thành công trong khoảng này thì dữ liệu bị coi là stale.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Cảnh báo khi một phía (mua/bán chủ động) lớn hơn phía kia ít nhất bấy nhiêu %.</summary>
    public decimal VolumeAlertExcessPercent { get; set; } = VolumePressure.DefaultAlertExcessPercent;
}
