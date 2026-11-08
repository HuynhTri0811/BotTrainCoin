namespace CryptoBot.Domain.Strategies;

/// <summary>Một chiến lược chạy trên một cặp/khung nến. Mỗi lần đổi tham số tăng Version.</summary>
public sealed class StrategyInstance
{
    /// <summary>Khóa chính do database cấp.</summary>
    public int Id { get; private set; }

    /// <summary>Tên duy nhất trong cấu hình, ví dụ "btc-1h-ema".</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Loại chiến lược, hiện có "EmaCross".</summary>
    public string StrategyType { get; private set; } = null!;

    /// <summary>Phiên bản cấu hình; tăng mỗi khi đổi tham số, cặp hoặc khung nến.</summary>
    public int Version { get; private set; }

    /// <summary>Tham số chiến lược dạng JSON chuẩn hóa (dùng để so sánh khi đổi cấu hình).</summary>
    public string ParametersJson { get; private set; } = null!;

    /// <summary>Sàn lấy dữ liệu, ví dụ "Binance".</summary>
    public string Exchange { get; private set; } = null!;

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Mã khung nến, ví dụ "1h".</summary>
    public string Interval { get; private set; } = null!;

    /// <summary>False thì worker không đánh giá chiến lược này.</summary>
    public bool Enabled { get; private set; }

    /// <summary>OpenTime của nến đóng cuối cùng đã đánh giá với Version hiện tại.</summary>
    public DateTime? LastEvaluatedCandleOpenTimeUtc { get; private set; }

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private StrategyInstance() { }

    /// <summary>Tạo strategy instance mới ở Version 1, chưa đánh giá nến nào.</summary>
    public StrategyInstance(string name, string strategyType, string parametersJson,
        string exchange, string symbol, string interval, bool enabled)
    {
        Name = name;
        StrategyType = strategyType;
        ParametersJson = parametersJson;
        Exchange = exchange;
        Symbol = symbol;
        Interval = interval;
        Enabled = enabled;
        Version = 1;
    }

    /// <summary>
    /// Áp dụng cấu hình. Đổi tham số/cặp/khung nến tạo version mới và đánh giá lại từ đầu
    /// dưới version đó; signal cũ vẫn giữ nguyên với version cũ.
    /// </summary>
    public bool Configure(string strategyType, string parametersJson, string exchange,
        string symbol, string interval, bool enabled)
    {
        bool definitionChanged = strategyType != StrategyType || parametersJson != ParametersJson ||
                                exchange != Exchange || symbol != Symbol || interval != Interval;
        bool changed = definitionChanged || enabled != Enabled;

        if (definitionChanged)
        {
            StrategyType = strategyType;
            ParametersJson = parametersJson;
            Exchange = exchange;
            Symbol = symbol;
            Interval = interval;
            Version++;
            LastEvaluatedCandleOpenTimeUtc = null;
        }

        Enabled = enabled;
        return changed;
    }

    /// <summary>Ghi nhận đã đánh giá nến này; nến phải mới hơn checkpoint để mỗi nến chỉ đánh giá một lần.</summary>
    public void MarkEvaluated(DateTime candleOpenTimeUtc)
    {
        if (LastEvaluatedCandleOpenTimeUtc is { } last && candleOpenTimeUtc <= last)
            throw new InvalidOperationException("Nến phải được đánh giá theo thứ tự tăng dần, mỗi nến một lần.");
        LastEvaluatedCandleOpenTimeUtc = candleOpenTimeUtc;
    }
}
