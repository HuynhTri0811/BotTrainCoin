using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Indicators;

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

    /// <summary>Tên loại viết đúng như trong <see cref="AvailableTypes"/>; null nếu không có loại này.</summary>
    public static string? CanonicalType(string? type) => type is null
        ? null
        : AvailableTypes.Keys.FirstOrDefault(name => string.Equals(name, type, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Giá trị hiện tại của mọi tham số (property public có setter) của <paramref name="indicator"/>, dạng chuỗi
    /// invariant như trong <c>appsettings.json</c>; đưa lại vào <see cref="Create"/> sẽ ra chỉ báo giống hệt.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ReadParameters(ICustomIndicator indicator)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in Settable(indicator.GetType()).Values.OrderBy(property => property.MetadataToken))
        {
            var value = property.GetValue(indicator);
            parameters[property.Name] = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value?.ToString() ?? "";
        }

        return parameters;
    }

    private static Dictionary<string, PropertyInfo> Settable(Type clr) => clr
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanWrite && property.SetMethod!.IsPublic)
        .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Tạo chỉ báo theo tên loại và gán tham số. Trả về lỗi nếu loại/tham số không hợp lệ.</summary>
    public static ICustomIndicator Create(string type, IReadOnlyDictionary<string, string?> parameters)
    {
        if (!AvailableTypes.TryGetValue(type, out var clr))
            throw new ArgumentException(
                $"Không có chỉ báo loại '{type}'. Các loại có sẵn: {string.Join(", ", AvailableTypes.Keys)}.");

        var indicator = (ICustomIndicator)(Activator.CreateInstance(clr)
            ?? throw new ArgumentException($"Không tạo được '{type}': cần constructor không tham số."));

        var props = Settable(clr);

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
            var type = CanonicalType(definition.Type)!;
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
