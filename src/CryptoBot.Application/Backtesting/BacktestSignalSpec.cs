using System.Text.Json;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>Nguồn tín hiệu đã dùng: EMA cross hoặc một loại chỉ báo tự viết, kèm tham số dạng chuỗi.</summary>
/// <param name="Type"><see cref="StrategyTypes.EmaCross"/> hoặc loại chỉ báo, ví dụ "MyRsi".</param>
/// <param name="Parameters">Tên tham số → giá trị (số dùng dấu chấm thập phân).</param>
/// <param name="RequiredCandles">Số nến của cửa sổ mỗi lần đọc tín hiệu.</param>
public sealed record BacktestSignalSpec(string Type, IReadOnlyDictionary<string, string> Parameters,
    int RequiredCandles)
{
    /// <summary>Có phải EMA cross không (lần chạy cũ chưa lưu nguồn tín hiệu cũng là EMA cross).</summary>
    public bool IsEmaCross => string.Equals(Type, StrategyTypes.EmaCross, StringComparison.OrdinalIgnoreCase);

    public static BacktestSignalSpec ForEmaCross(EmaCrossParameters parameters) => new(StrategyTypes.EmaCross,
        new Dictionary<string, string>
        {
            [nameof(EmaCrossParameters.FastPeriod)] = parameters.FastPeriod.ToString(),
            [nameof(EmaCrossParameters.SlowPeriod)] = parameters.SlowPeriod.ToString(),
            [nameof(EmaCrossParameters.LookbackCandles)] = parameters.LookbackCandles.ToString(),
        }, parameters.LookbackCandles);

    /// <summary>Tham số dạng JSON để lưu vào một cột.</summary>
    public string ParametersJson => JsonSerializer.Serialize(Parameters);

    /// <summary>Đọc lại tham số đã lưu; JSON trống hoặc hỏng thành danh sách rỗng (chỉ để hiển thị).</summary>
    public static IReadOnlyDictionary<string, string> ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
