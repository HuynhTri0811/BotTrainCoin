using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Indicators;

/// <summary>Một mục trong <c>appsettings.json</c> → <c>Indicators</c>.</summary>
public sealed class IndicatorDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Exchange { get; set; } = "Binance";
    public string Symbol { get; set; } = "";
    public string Interval { get; set; } = "15m";
    public bool Enabled { get; set; } = true;

    /// <summary>Tên property của chỉ báo → giá trị (chuỗi, số dùng dấu chấm thập phân).</summary>
    public Dictionary<string, string?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ConfiguredIndicator(
    string Name,
    string Type,
    string Description,
    CandleKey Key,
    ICustomIndicator Indicator);

public sealed record IndicatorConfigError(string Name, string Error);

/// <summary>
/// Tìm mọi class có <see cref="IndicatorAttribute"/> trong assembly Domain và tạo chỉ báo theo cấu hình.
/// Cấu hình sai chỉ bị bỏ qua (kèm lỗi rõ ràng), không làm dừng ứng dụng.
/// </summary>
public sealed class IndicatorRegistry
{
    private readonly Dictionary<string, ConfiguredIndicator> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IndicatorConfigError> _errors = [];

    /// <summary>Các loại chỉ báo tìm thấy: tên loại → class.</summary>
    public static IReadOnlyDictionary<string, Type> AvailableTypes { get; } = typeof(ICustomIndicator).Assembly
        .GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ICustomIndicator).IsAssignableFrom(type))
        .Select(type => (Type: type, Attr: type.GetCustomAttribute<IndicatorAttribute>()))
        .Where(entry => entry.Attr is not null)
        .ToDictionary(entry => entry.Attr!.Type, entry => entry.Type, StringComparer.OrdinalIgnoreCase);

    public IndicatorRegistry(IEnumerable<IndicatorDefinition> definitions, ILogger<IndicatorRegistry> logger)
    {
        foreach (var definition in definitions.Where(candidate => candidate.Enabled))
        {
            if (TryBuild(definition, out var configured, out var error) && _byName.TryAdd(definition.Name, configured!))
            {
                logger.LogInformation("Chỉ báo '{Name}' ({Type}) trên {Key}", definition.Name, configured!.Type,
                    configured.Key);
                continue;
            }

            error ??= $"Tên chỉ báo '{definition.Name}' bị trùng.";
            _errors.Add(new IndicatorConfigError(definition.Name, error));
            logger.LogError("Bỏ qua chỉ báo '{Name}': {Error}", definition.Name, error);
        }
    }

    public IReadOnlyCollection<ConfiguredIndicator> All => _byName.Values;
    public IReadOnlyList<IndicatorConfigError> Errors => _errors;

    public ConfiguredIndicator? Find(string? name) =>
        name is not null && _byName.TryGetValue(name, out var indicator) ? indicator : null;

    /// <summary>Tạo chỉ báo theo tên loại và gán tham số. Trả về lỗi nếu loại/tham số không hợp lệ.</summary>
    public static ICustomIndicator Create(string type, IReadOnlyDictionary<string, string?> parameters)
    {
        if (!AvailableTypes.TryGetValue(type, out var clr))
            throw new ArgumentException(
                $"Không có chỉ báo loại '{type}'. Các loại có sẵn: {string.Join(", ", AvailableTypes.Keys)}.");

        var indicator = (ICustomIndicator)(Activator.CreateInstance(clr)
            ?? throw new ArgumentException($"Không tạo được '{type}': cần constructor không tham số."));

        var props = clr.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite && property.SetMethod!.IsPublic)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, raw) in parameters)
        {
            if (!props.TryGetValue(key, out var prop))
                throw new ArgumentException(
                    $"Tham số '{key}' không tồn tại trong {clr.Name}. Có: {string.Join(", ", props.Keys)}.");
            try
            {
                var converter = TypeDescriptor.GetConverter(prop.PropertyType);
                prop.SetValue(indicator, raw is null ? null : converter.ConvertFromString(null, CultureInfo.InvariantCulture, raw));
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException
                                           || ex.InnerException is FormatException or OverflowException)
            {
                throw new ArgumentException($"Tham số '{key}' = '{raw}' không đúng kiểu {prop.PropertyType.Name}.");
            }
        }

        if (indicator.Validate() is { } error)
            throw new ArgumentException(error);
        return indicator;
    }

    private static bool TryBuild(IndicatorDefinition definition, out ConfiguredIndicator? configured, out string? error)
    {
        configured = null;
        error = null;
        if (string.IsNullOrWhiteSpace(definition.Name))
            error = "Thiếu Name.";
        else if (string.IsNullOrWhiteSpace(definition.Symbol) || !definition.Symbol.All(char.IsAsciiLetterOrDigit))
            error = "Symbol không hợp lệ.";
        else if (!CandleInterval.TryParse(definition.Interval, out _))
            error = $"Interval không hỗ trợ: '{definition.Interval}'.";
        if (error is not null)
            return false;

        try
        {
            var indicator = Create(definition.Type, definition.Parameters);
            var type = AvailableTypes.Keys.First(typeName => string.Equals(typeName, definition.Type,
                StringComparison.OrdinalIgnoreCase));
            var description = AvailableTypes[type].GetCustomAttribute<IndicatorAttribute>()!.Description;
            configured = new ConfiguredIndicator(definition.Name, type, description,
                new CandleKey(definition.Exchange, definition.Symbol.ToUpperInvariant(),
                    CandleInterval.Parse(definition.Interval)), indicator);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
