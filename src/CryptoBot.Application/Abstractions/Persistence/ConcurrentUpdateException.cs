namespace CryptoBot.Application.Abstractions;

/// <summary>
/// Bản ghi đã bị một bên khác (DbContext/tiến trình khác) thay đổi sau khi được đọc, nên thay đổi đang lưu bị từ chối.
/// Các thay đổi chưa lưu của bản ghi đó đã được bỏ và trạng thái được đọc lại từ database.
/// </summary>
public sealed class ConcurrentUpdateException(string message, Exception? inner = null) : Exception(message, inner);
