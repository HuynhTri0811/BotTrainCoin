namespace CryptoBot.Api.Endpoints;

/// <summary>Tải nến từ <see cref="FromUtc"/> đến trước nến sớm nhất đã lưu.</summary>
public sealed record BackfillRequestDto(DateTimeOffset FromUtc);
