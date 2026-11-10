namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Ngày nghỉ lễ của từng sàn, chỉ ghi các ngày rơi vào Thứ Hai đến Thứ Sáu. Thêm năm mới thì nâng
/// <see cref="StockMarket.HolidaysKnownThroughYear"/> trong <see cref="StockMarkets"/>.
/// Nguồn và các ngày còn mâu thuẫn ghi trong docs/features.md, mục Dự đoán nến kế tiếp.
/// </summary>
public static class StockMarketHolidays
{
    /// <summary>NYSE 2026–2027, theo thông báo của NYSE Group ngày 23/12/2025.</summary>
    public static readonly IReadOnlySet<DateOnly> UnitedStates = Dates(
        "2026-01-01", "2026-01-19", "2026-02-16", "2026-04-03", "2026-05-25", "2026-06-19", "2026-07-03",
        "2026-09-07", "2026-11-26", "2026-12-25",
        "2027-01-01", "2027-01-18", "2027-02-15", "2027-03-26", "2027-05-31", "2027-06-18", "2027-07-05",
        "2027-09-06", "2027-11-25", "2027-12-24");

    /// <summary>LSE 2026–2027: ngày nghỉ ngân hàng của Anh, kể cả ngày nghỉ bù khi lễ rơi vào cuối tuần.</summary>
    public static readonly IReadOnlySet<DateOnly> UnitedKingdom = Dates(
        "2026-01-01", "2026-04-03", "2026-04-06", "2026-05-04", "2026-05-25", "2026-08-31", "2026-12-25",
        "2026-12-28",
        "2027-01-01", "2027-03-26", "2027-03-29", "2027-05-03", "2027-05-31", "2027-08-30", "2027-12-27",
        "2027-12-28");

    /// <summary>TSE 2026–2027: ngày lễ quốc gia Nhật, nghỉ bù, và nghỉ cuối năm 31/12 đến 03/01.</summary>
    public static readonly IReadOnlySet<DateOnly> Japan = Dates(
        "2026-01-01", "2026-01-02", "2026-01-12", "2026-02-11", "2026-02-23", "2026-03-20", "2026-04-29",
        "2026-05-04", "2026-05-05", "2026-05-06", "2026-07-20", "2026-08-11", "2026-09-21", "2026-09-22",
        "2026-09-23", "2026-10-12", "2026-11-03", "2026-11-23", "2026-12-31",
        "2027-01-01", "2027-01-11", "2027-02-11", "2027-02-23", "2027-03-22", "2027-04-29", "2027-05-03",
        "2027-05-04", "2027-05-05", "2027-07-19", "2027-08-11", "2027-09-20", "2027-09-23", "2027-10-11",
        "2027-11-03", "2027-11-23", "2027-12-31");

    /// <summary>
    /// KRX 2026: các ngày mọi nguồn thống nhất. Chưa ghi các ngày nguồn còn mâu thuẫn: 02/03, 01/05, 25/05, 03/06,
    /// 17/08, 05/10.
    /// </summary>
    public static readonly IReadOnlySet<DateOnly> Korea = Dates(
        "2026-01-01", "2026-02-16", "2026-02-17", "2026-02-18", "2026-05-05", "2026-09-24", "2026-09-25",
        "2026-10-09", "2026-12-25", "2026-12-31");

    /// <summary>
    /// HOSE 2026, theo thông báo 2294/TB-SGDHCM. Chưa ghi 02/01 vì nguồn còn mâu thuẫn.
    /// </summary>
    public static readonly IReadOnlySet<DateOnly> Vietnam = Dates(
        "2026-01-01", "2026-02-16", "2026-02-17", "2026-02-18", "2026-02-19", "2026-02-20", "2026-04-27",
        "2026-04-30", "2026-05-01", "2026-08-31", "2026-09-01", "2026-09-02");

    private static HashSet<DateOnly> Dates(params string[] isoDates) =>
        isoDates.Select(isoDate => DateOnly.ParseExact(isoDate, "yyyy-MM-dd")).ToHashSet();
}
