namespace CryptoBot.Domain.Strategies;

/// <summary>Hai đường EMA vừa cắt nhau theo hướng nào ở nến đang xét.</summary>
public enum EmaCrossDirection
{
    /// <summary>Không có giao cắt (hoặc chưa đủ nến).</summary>
    None = 0,

    /// <summary>EMA nhanh cắt lên EMA chậm.</summary>
    Up = 1,

    /// <summary>EMA nhanh cắt xuống EMA chậm.</summary>
    Down = 2,
}
