using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

/// <param name="BlockedReason">
/// Lý do bị chặn bởi kiểm tra trước khi mở (ví dụ giới hạn lỗ ngày); khi có thì không mở vị thế.
/// </param>
public sealed record OpenResult(OpenQuote Quote, FuturesPosition? Position, bool Duplicate,
    string? BlockedReason = null);
