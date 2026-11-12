namespace CryptoBot.Api.Endpoints;

/// <summary>Nguồn tín hiệu đã dùng: "EmaCross" hoặc loại chỉ báo tự viết, kèm tham số.</summary>
public sealed record BacktestSignalDto(string Type, IReadOnlyDictionary<string, string> Parameters);
