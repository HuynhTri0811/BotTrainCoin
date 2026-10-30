namespace CryptoBot.Domain.Futures;

/// <summary>Quy tắc kích hoạt theo mức giá hay theo tín hiệu của một chỉ báo tự viết.</summary>
public enum RuleTriggerType
{
    /// <summary>Kích hoạt khi mark price chạm mức giá đặt.</summary>
    Price = 1,

    /// <summary>Kích hoạt khi chỉ báo tự viết phát tín hiệu cùng hướng với quy tắc.</summary>
    Indicator = 2,
}
