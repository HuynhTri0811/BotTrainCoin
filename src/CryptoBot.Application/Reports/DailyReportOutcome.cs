namespace CryptoBot.Application.Reports;

/// <summary>Kết quả lập báo cáo của một (symbol, giờ lập).</summary>
public enum DailyReportOutcome
{
    /// <summary>Đã có báo cáo đủ nến; không lập lại.</summary>
    AlreadyComplete,

    /// <summary>Đã lưu báo cáo đủ nến (mới, hoặc hoàn thiện báo cáo thiếu nến trước đó).</summary>
    Saved,

    /// <summary>Đã lưu nhưng sàn mới trả một phần nến; sẽ lập lại ở lần chạy sau.</summary>
    SavedIncomplete,
}
