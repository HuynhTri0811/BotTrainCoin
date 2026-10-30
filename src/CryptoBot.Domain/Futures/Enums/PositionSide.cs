namespace CryptoBot.Domain.Futures;

/// <summary>Hướng của vị thế Futures.</summary>
public enum PositionSide
{
    /// <summary>Mua trước, lãi khi giá tăng.</summary>
    Long = 1,

    /// <summary>Bán trước, lãi khi giá giảm.</summary>
    Short = 2,
}
