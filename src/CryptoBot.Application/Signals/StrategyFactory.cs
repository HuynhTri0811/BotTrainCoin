using System.Text.Json;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Signals;

public static class StrategyTypes
{
    public const string EmaCross = "EmaCross";
}

public static class StrategyFactory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static EmaCrossStrategy Create(StrategyInstance instance) => instance.StrategyType switch
    {
        StrategyTypes.EmaCross => new EmaCrossStrategy(ParseEmaCross(instance.ParametersJson)),
        _ => throw new NotSupportedException($"Strategy type không được hỗ trợ: {instance.StrategyType}"),
    };

    public static EmaCrossParameters ParseEmaCross(string json) =>
        JsonSerializer.Deserialize<EmaCrossParameters>(json, Json)
        ?? throw new ArgumentException("Thiếu tham số chiến lược.");

    /// <summary>Chuỗi JSON chuẩn hóa để so sánh cấu hình và quyết định tăng version.</summary>
    public static string Serialize(EmaCrossParameters parameters) => JsonSerializer.Serialize(parameters, Json);
}
