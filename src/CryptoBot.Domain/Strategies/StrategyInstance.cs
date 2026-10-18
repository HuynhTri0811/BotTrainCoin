namespace CryptoBot.Domain.Strategies;

/// <summary>Một chiến lược chạy trên một cặp/khung nến. Mỗi lần đổi tham số tăng Version.</summary>
public sealed class StrategyInstance
{
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string StrategyType { get; private set; } = null!;
    public int Version { get; private set; }
    public string ParametersJson { get; private set; } = null!;
    public string Exchange { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public string Interval { get; private set; } = null!;
    public bool Enabled { get; private set; }

    /// <summary>OpenTime của nến đóng cuối cùng đã đánh giá với Version hiện tại.</summary>
    public DateTime? LastEvaluatedCandleOpenTimeUtc { get; private set; }

    private StrategyInstance() { }

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
        var definitionChanged = strategyType != StrategyType || parametersJson != ParametersJson ||
                                exchange != Exchange || symbol != Symbol || interval != Interval;
        var changed = definitionChanged || enabled != Enabled;

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

    public void MarkEvaluated(DateTime candleOpenTimeUtc)
    {
        if (LastEvaluatedCandleOpenTimeUtc is { } last && candleOpenTimeUtc <= last)
            throw new InvalidOperationException("Nến phải được đánh giá theo thứ tự tăng dần, mỗi nến một lần.");
        LastEvaluatedCandleOpenTimeUtc = candleOpenTimeUtc;
    }
}
