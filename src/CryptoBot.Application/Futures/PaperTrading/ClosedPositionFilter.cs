using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

/// <summary>Điều kiện lọc vị thế đã đóng; trường null là không lọc. Thời gian tính theo giờ đóng, [FromUtc, ToUtc).</summary>
public sealed record ClosedPositionFilter(
    string? Symbol = null,
    PositionSide? Side = null,
    CloseReason? CloseReason = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);
