namespace CryptoBot.Domain.MarketData;

/// <summary>Phía nào đang áp đảo trong một nến.</summary>
public enum VolumePressureSide
{
    /// <summary>Chênh lệch mua/bán chưa tới ngưỡng cảnh báo.</summary>
    Balanced,

    /// <summary>Mua chủ động lớn hơn bán chủ động từ ngưỡng trở lên.</summary>
    BuyHeavy,

    /// <summary>Bán chủ động lớn hơn mua chủ động từ ngưỡng trở lên.</summary>
    SellHeavy,
}
