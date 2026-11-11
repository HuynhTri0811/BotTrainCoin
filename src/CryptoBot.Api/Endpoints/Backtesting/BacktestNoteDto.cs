namespace CryptoBot.Api.Endpoints;

/// <summary>Ghi chú cho một lần backtest đã lưu; null hoặc chuỗi trống = xóa ghi chú.</summary>
public sealed record BacktestNoteDto(string? Note);
