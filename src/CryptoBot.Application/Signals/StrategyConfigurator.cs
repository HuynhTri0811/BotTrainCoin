using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Signals;

public sealed class StrategyDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = StrategyTypes.EmaCross;
    public string Exchange { get; set; } = "Binance";
    public string Symbol { get; set; } = "";
    public string Interval { get; set; } = "1h";
    public bool Enabled { get; set; } = true;
    public EmaCrossParameters Parameters { get; set; } = new();
}

/// <summary>Đồng bộ strategy instance trong DB với cấu hình khai báo. Cấu hình sai bị từ chối.</summary>
public sealed class StrategyConfigurator(IStrategyInstanceStore store, ILogger<StrategyConfigurator> logger)
{
    public async Task ApplyAsync(IReadOnlyList<StrategyDefinition> definitions, CancellationToken ct)
    {
        foreach (var d in definitions)
        {
            if (Validate(d) is { } error)
            {
                logger.LogError("Bỏ qua cấu hình chiến lược '{Name}': {Error}", d.Name, error);
                continue;
            }

            var json = StrategyFactory.Serialize(d.Parameters);
            var existing = await store.FindByNameAsync(d.Name, ct);
            if (existing is null)
            {
                await store.AddAsync(new StrategyInstance(d.Name, d.Type, json, d.Exchange, d.Symbol, d.Interval, d.Enabled), ct);
                logger.LogInformation("Tạo strategy instance '{Name}' {Symbol} {Interval}", d.Name, d.Symbol, d.Interval);
            }
            else
            {
                var oldVersion = existing.Version;
                if (existing.Configure(d.Type, json, d.Exchange, d.Symbol, d.Interval, d.Enabled) &&
                    existing.Version != oldVersion)
                {
                    logger.LogInformation("Strategy '{Name}' đổi cấu hình: v{Old} -> v{New}", d.Name, oldVersion, existing.Version);
                }
            }
        }

        await store.SaveAsync(ct);
    }

    private static string? Validate(StrategyDefinition d)
    {
        if (string.IsNullOrWhiteSpace(d.Name))
            return "Thiếu Name.";
        if (d.Type != StrategyTypes.EmaCross)
            return $"Type không hỗ trợ: {d.Type}.";
        if (string.IsNullOrWhiteSpace(d.Symbol) || !d.Symbol.All(char.IsAsciiLetterOrDigit))
            return "Symbol không hợp lệ.";
        if (!CandleInterval.TryParse(d.Interval, out _))
            return $"Interval không hỗ trợ: {d.Interval}.";
        return d.Parameters.Validate();
    }
}
