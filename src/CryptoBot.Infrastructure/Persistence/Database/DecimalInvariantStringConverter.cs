using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class DecimalInvariantStringConverter() : ValueConverter<decimal, string>(
    value => value.ToString(CultureInfo.InvariantCulture),
    value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture));
