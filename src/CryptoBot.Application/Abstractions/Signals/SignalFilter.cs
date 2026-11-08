using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Abstractions;

/// <summary>Điều kiện lọc tín hiệu; trường null là không lọc. Thời gian tính theo giờ mở nến, gồm cả hai đầu.</summary>
public sealed record SignalFilter(
    int? StrategyInstanceId = null,
    int? ConfigVersion = null,
    SignalAction? Action = null,
    bool? IsBackfill = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);
