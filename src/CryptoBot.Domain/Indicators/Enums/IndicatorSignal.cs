namespace CryptoBot.Domain.Indicators;

/// <summary>Tín hiệu một chỉ báo phát ra tại một nến đã đóng.</summary>
public enum IndicatorSignal
{
    /// <summary>Không có tín hiệu ở nến này.</summary>
    None = 0,

    /// <summary>Nghiêng về mở Long.</summary>
    Long = 1,

    /// <summary>Nghiêng về mở Short.</summary>
    Short = 2,
}
