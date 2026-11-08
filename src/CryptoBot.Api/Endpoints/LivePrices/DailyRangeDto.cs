namespace CryptoBot.Api.Endpoints;

/// <summary>Biên độ dự kiến sau HorizonDays ngày (xem DailyMoveRange). Số chỉ để hiển thị.</summary>
public sealed record DailyRangeDto(string Symbol, int HorizonDays, int Days, decimal Price,
    decimal TypicalMovePercent, decimal Low68, decimal High68, decimal Low95, decimal High95,
    decimal AverageMovePercent, decimal LargestRisePercent, decimal LargestDropPercent, decimal UpDaysPercent,
    int Samples, SimilarDaysDto? SimilarDays);
