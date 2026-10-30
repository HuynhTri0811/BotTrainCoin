using System.Net;
using System.Text.RegularExpressions;

namespace CryptoBot.Domain.News;

/// <summary>Xử lý chữ của tin tức: bỏ HTML trong tóm tắt và nhận ra coin được nhắc tới.</summary>
public static partial class NewsText
{
    /// <summary>Coin được nhận diện, theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<string> Coins = ["BTC", "ETH", "ETC"];

    /// <summary>
    /// Bỏ thẻ HTML, giải mã ký tự (<c>&amp;amp;</c>…), gộp khoảng trắng, cắt còn tối đa <paramref name="maxLength"/> ký tự
    /// (thêm "…" khi bị cắt).
    /// </summary>
    public static string ToPlainText(string? html, int maxLength)
    {
        if (maxLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Độ dài tối đa phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(html))
            return "";

        string text = WebUtility.HtmlDecode(Tags().Replace(html, " "));
        text = Spaces().Replace(text, " ").Trim();
        return text.Length <= maxLength ? text : text[..(maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// Coin được nhắc tới trong <paramref name="text"/> (tiêu đề + tóm tắt), theo từ nguyên vẹn, không phân biệt hoa
    /// thường. "Ethereum Classic" chỉ tính là ETC, không tính là ETH.
    /// </summary>
    public static IReadOnlyList<string> CoinsIn(string text)
    {
        List<string> coins = [];
        string withoutClassic = EthereumClassic().Replace(text, " ");
        if (Bitcoin().IsMatch(text))
            coins.Add("BTC");
        if (Ether().IsMatch(withoutClassic))
            coins.Add("ETH");
        if (EthereumClassic().IsMatch(text) || Etc().IsMatch(text))
            coins.Add("ETC");
        return coins;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b(bitcoins?|btc)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Bitcoin();

    [GeneratedRegex(@"\b(ether|ethereum|eth)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Ether();

    [GeneratedRegex(@"\bethereum\s+classic\b", RegexOptions.IgnoreCase)]
    private static partial Regex EthereumClassic();

    // "ETC" viết hoa mới tính: "etc." trong câu tiếng Anh là "vân vân".
    [GeneratedRegex(@"\bETC\b")]
    private static partial Regex Etc();
}
