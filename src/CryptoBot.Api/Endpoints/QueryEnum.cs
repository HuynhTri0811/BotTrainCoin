namespace CryptoBot.Api.Endpoints;

/// <summary>Đọc enum từ query string theo tên, không phân biệt hoa thường.</summary>
internal static class QueryEnum
{
    /// <summary>Thiếu tham số là hợp lệ (không lọc). Số như "1" bị từ chối vì không phải tên enum.</summary>
    public static bool TryParse<T>(string? text, out T? value) where T : struct, Enum
    {
        value = null;
        if (text is null)
            return true;
        var name = Enum.GetNames<T>().FirstOrDefault(candidate => candidate.Equals(text,
            StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return false;
        value = Enum.Parse<T>(name);
        return true;
    }

    public static string Error<T>(string text) where T : struct, Enum =>
        $"Giá trị không hỗ trợ: {text}. Chọn {string.Join(", ", Enum.GetNames<T>())}.";
}
