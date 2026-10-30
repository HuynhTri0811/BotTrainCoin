namespace CryptoBot.Domain.Futures;

/// <summary>Hướng so sánh giữa mark price và giá đặt của quy tắc theo giá.</summary>
public enum TriggerCondition
{
    /// <summary>Kích hoạt khi mark price ≤ giá đặt (ví dụ mua khi giá giảm về một mức).</summary>
    AtOrBelow = 1,

    /// <summary>Kích hoạt khi mark price ≥ giá đặt.</summary>
    AtOrAbove = 2,
}
