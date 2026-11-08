namespace CryptoBot.Domain.MarketData;

/// <summary>Một lỗi trong chuỗi nến, gắn với nến có OpenTime <paramref name="OpenTimeUtc"/>.</summary>
public sealed record CandleIssue(CandleIssueKind Kind, DateTime OpenTimeUtc, string Detail)
{
    /// <summary>
    /// True khi nến mang lỗi này không được lưu (giá trị sai, trùng, sai thứ tự). Gap chỉ được ghi nhận:
    /// nến sau khoảng thiếu vẫn đúng.
    /// </summary>
    public bool RejectsCandle => Kind is not CandleIssueKind.Gap;
}
