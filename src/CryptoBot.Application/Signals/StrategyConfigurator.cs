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
        foreach (var definition in definitions)
        {
            if (Validate(definition) is { } error)
            {
                logger.LogError("Bỏ qua cấu hình chiến lược '{Name}': {Error}", definition.Name, error);
                continue;
            }

            var json = StrategyFactory.Serialize(definition.Parameters);
            var existing = await store.FindByNameAsync(definition.Name, ct);
            if (existing is null)
            {
                await store.AddAsync(new StrategyInstance(definition.Name, definition.Type, json, definition.Exchange,
                    definition.Symbol, definition.Interval, definition.Enabled), ct);
                logger.LogInformation("Tạo strategy instance '{Name}' {Symbol} {Interval}", definition.Name,
                    definition.Symbol, definition.Interval);
            }
            else
            {
                var oldVersion = existing.Version;
                if (existing.Configure(definition.Type, json, definition.Exchange, definition.Symbol,
                    definition.Interval, definition.Enabled) &&
                    existing.Version != oldVersion)
                {
                    logger.LogInformation("Strategy '{Name}' đổi cấu hình: v{Old} -> v{New}", definition.Name,
                        oldVersion, existing.Version);
                }
            }
        }

        await store.SaveAsync(ct);
    }

    private static string? Validate(StrategyDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            return "Thiếu Name.";
        if (definition.Type != StrategyTypes.EmaCross)
            return $"Type không hỗ trợ: {definition.Type}.";
        if (string.IsNullOrWhiteSpace(definition.Symbol) || !definition.Symbol.All(char.IsAsciiLetterOrDigit))
            return "Symbol không hợp lệ.";
        if (!CandleInterval.TryParse(definition.Interval, out _))
            return $"Interval không hỗ trợ: {definition.Interval}.";
        return definition.Parameters.Validate();
    }
}
