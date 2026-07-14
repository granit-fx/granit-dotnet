using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Granit.DataExchange.Import.Parsing;

namespace Granit.DataExchange.Import.Mapping.Internal;

/// <summary>
/// Default <see cref="IDataMapper{TEntity}"/> compiled once from an
/// <see cref="ImportDefinition{TEntity}"/>: expression-tree setters, TryParse-first converters.
/// Bad cell values never throw — they surface as <see cref="CellConversionError"/> entries.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <remarks>
/// Structural problems (missing property, no public setter, no public parameterless constructor,
/// unsupported property type) are definition bugs and fail fast at construction with an
/// actionable message. Data problems (unparseable cells, missing required values) are row data
/// and come back as errors in the <see cref="MappingResult{TEntity}"/>.
/// </remarks>
internal sealed class CompiledDataMapper<TEntity> : IDataMapper<TEntity> where TEntity : class
{
    private const string ErrorCodePrefix = "Granit:DataExchange:Conversion:";
    internal const string InvalidFormatErrorCode = ErrorCodePrefix + "InvalidFormat";
    internal const string MissingRequiredErrorCode = ErrorCodePrefix + "MissingRequired";
    internal const string OverflowErrorCode = ErrorCodePrefix + "Overflow";
    internal const string UnknownPropertyErrorCode = ErrorCodePrefix + "UnknownProperty";
    internal const string UnsupportedErrorCode = ErrorCodePrefix + "Unsupported";
    internal const string UnexpectedErrorCode = ErrorCodePrefix + "Unexpected";

    private readonly Func<TEntity> _factory;
    private readonly Dictionary<string, CompiledProperty> _properties;

    public CompiledDataMapper(ImportDefinition<TEntity> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ConstructorInfo? constructor = typeof(TEntity).GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"Import definition '{definition.Name}' targets entity type '{typeof(TEntity).Name}' " +
                "which has no public parameterless constructor. " +
                $"Add a parameterless constructor or register a custom IDataMapper<{typeof(TEntity).Name}>.");
        }

        _factory = Expression.Lambda<Func<TEntity>>(Expression.New(constructor)).Compile();
        _properties = new Dictionary<string, CompiledProperty>(StringComparer.Ordinal);

        foreach (PropertyMapping mapping in definition.GetProperties())
        {
            _properties[mapping.PropertyPath] = CompileProperty(definition.Name, mapping);
        }
    }

    /// <inheritdoc/>
    public Task<MappingResult<TEntity>> MapAsync(
        RawImportRow row,
        IReadOnlyList<ImportColumnMapping> mappings,
        ImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(mappings);

        try
        {
            return Task.FromResult(Map(row, mappings));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Defensive net: one broken row must never kill the whole job.
            return Task.FromResult(new MappingResult<TEntity>
            {
                Errors =
                [
                    new CellConversionError("*", "*", ex.Message, typeof(TEntity).Name, UnexpectedErrorCode),
                ],
            });
        }
    }

    private MappingResult<TEntity> Map(RawImportRow row, IReadOnlyList<ImportColumnMapping> mappings)
    {
        List<ImportColumnMapping> confirmed = [.. mappings.Where(m => m.TargetProperty is not null)];

        // Empty-row signal: every mapped cell blank → Entity null with zero errors (pipeline emits Skipped).
        if (confirmed.TrueForAll(m => string.IsNullOrWhiteSpace(GetCell(row, m.SourceColumn))))
        {
            return new MappingResult<TEntity>();
        }

        TEntity entity = _factory();
        List<CellConversionError> errors = [];

        foreach (ImportColumnMapping mapping in confirmed)
        {
            string targetProperty = mapping.TargetProperty!;
            string? rawValue = GetCell(row, mapping.SourceColumn);

            if (!_properties.TryGetValue(targetProperty, out CompiledProperty? property))
            {
                errors.Add(new CellConversionError(
                    mapping.SourceColumn, targetProperty, rawValue, "unknown", UnknownPropertyErrorCode));
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (property.Mapping.IsRequired)
                {
                    errors.Add(new CellConversionError(
                        mapping.SourceColumn, targetProperty, rawValue, property.ExpectedType, MissingRequiredErrorCode));
                }

                continue;
            }

            ConversionResult conversion = property.Converter(rawValue);
            if (conversion.ErrorCode is not null)
            {
                errors.Add(new CellConversionError(
                    mapping.SourceColumn, targetProperty, rawValue, property.ExpectedType, conversion.ErrorCode));
                continue;
            }

            property.Setter(entity, conversion.Value);
        }

        return errors.Count > 0
            ? new MappingResult<TEntity> { Errors = errors.AsReadOnly() }
            : new MappingResult<TEntity> { Entity = entity };
    }

    private static string? GetCell(RawImportRow row, string sourceColumn) =>
        row.Values.TryGetValue(sourceColumn, out string? value) ? value : null;

    private static CompiledProperty CompileProperty(string definitionName, PropertyMapping mapping)
    {
        PropertyInfo? propertyInfo = typeof(TEntity).GetProperty(
            mapping.PropertyPath, BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            throw new InvalidOperationException(
                $"Import definition '{definitionName}' declares property '{mapping.PropertyPath}' " +
                $"which does not exist on entity type '{typeof(TEntity).Name}'. " +
                $"Fix the definition or register a custom IDataMapper<{typeof(TEntity).Name}>.");
        }

        if (propertyInfo.GetSetMethod(nonPublic: false) is null)
        {
            throw new InvalidOperationException(
                $"Import definition '{definitionName}' declares property '{mapping.PropertyPath}' " +
                $"on entity type '{typeof(TEntity).Name}' which has no public setter. " +
                $"Add a public setter or register a custom IDataMapper<{typeof(TEntity).Name}>.");
        }

        Func<string, ConversionResult>? converter = CreateConverter(propertyInfo.PropertyType, mapping.Format);
        if (converter is null)
        {
            throw new InvalidOperationException(
                $"Import definition '{definitionName}' declares property '{mapping.PropertyPath}' " +
                $"of unsupported type '{propertyInfo.PropertyType.Name}' (error code '{UnsupportedErrorCode}'). " +
                $"Register a custom IDataMapper<{typeof(TEntity).Name}> to handle it.");
        }

        return new CompiledProperty(mapping, propertyInfo.PropertyType.Name, converter, CompileSetter(propertyInfo));
    }

    private static Action<TEntity, object?> CompileSetter(PropertyInfo propertyInfo)
    {
        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "entity");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");

        Expression body = Expression.Assign(
            Expression.Property(entity, propertyInfo),
            Expression.Convert(value, propertyInfo.PropertyType));

        return Expression.Lambda<Action<TEntity, object?>>(body, entity, value).Compile();
    }

    private static Func<string, ConversionResult>? CreateConverter(Type propertyType, string? format)
    {
        Type type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (type == typeof(string))
        {
            return static value => ConversionResult.Success(value);
        }

        if (type.IsEnum)
        {
            return CreateEnumConverter(type);
        }

        return CreateScalarConverter(type, format);
    }

    private static Func<string, ConversionResult>? CreateScalarConverter(Type type, string? format) =>
        type switch
        {
            _ when type == typeof(bool) => static value =>
                bool.TryParse(value.Trim(), out bool result)
                    ? ConversionResult.Success(result)
                    : ConversionResult.Failure(InvalidFormatErrorCode),
            _ when type == typeof(Guid) => static value =>
                Guid.TryParse(value.Trim(), out Guid result)
                    ? ConversionResult.Success(result)
                    : ConversionResult.Failure(InvalidFormatErrorCode),
            _ when type == typeof(int) => static value =>
                ConvertIntegral(value, int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result), result, int.MinValue, int.MaxValue),
            _ when type == typeof(long) => static value =>
                ConvertIntegral(value, long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result), result, long.MinValue, long.MaxValue),
            _ when type == typeof(short) => static value =>
                ConvertIntegral(value, short.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out short result), result, short.MinValue, short.MaxValue),
            _ when type == typeof(byte) => static value =>
                ConvertIntegral(value, byte.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result), result, byte.MinValue, byte.MaxValue),
            _ when type == typeof(decimal) => static value => ConvertDecimal(value),
            _ when type == typeof(double) => static value => ConvertFloatingPoint(
                double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result), result, double.IsFinite(result)),
            _ when type == typeof(float) => static value => ConvertFloatingPoint(
                float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result), result, float.IsFinite(result)),
            _ when type == typeof(DateTime) => value => ConvertTemporal<DateTime>(
                value, format,
                static (v, f) => (DateTime.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime r), r),
                static (v, culture) => (DateTime.TryParse(v, culture, DateTimeStyles.None, out DateTime r), r)),
            _ when type == typeof(DateTimeOffset) => value => ConvertTemporal<DateTimeOffset>(
                value, format,
                static (v, f) => (DateTimeOffset.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset r), r),
                static (v, culture) => (DateTimeOffset.TryParse(v, culture, DateTimeStyles.None, out DateTimeOffset r), r)),
            _ when type == typeof(DateOnly) => value => ConvertTemporal<DateOnly>(
                value, format,
                static (v, f) => (DateOnly.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly r), r),
                static (v, culture) => (DateOnly.TryParse(v, culture, DateTimeStyles.None, out DateOnly r), r)),
            _ when type == typeof(TimeOnly) => value => ConvertTemporal<TimeOnly>(
                value, format,
                static (v, f) => (TimeOnly.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly r), r),
                static (v, culture) => (TimeOnly.TryParse(v, culture, DateTimeStyles.None, out TimeOnly r), r)),
            _ => null,
        };

    private static Func<string, ConversionResult> CreateEnumConverter(Type enumType)
    {
        bool isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);
        return value =>
            Enum.TryParse(enumType, value.Trim(), ignoreCase: true, out object? result)
                && (isFlags || Enum.IsDefined(enumType, result!))
                ? ConversionResult.Success(result)
                : ConversionResult.Failure(InvalidFormatErrorCode);
    }

    private static ConversionResult ConvertIntegral(string value, bool parsed, object result, decimal min, decimal max)
    {
        if (parsed)
        {
            return ConversionResult.Success(result);
        }

        // Distinguish out-of-range integers (Overflow) from garbage (InvalidFormat).
        return decimal.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out decimal probe)
            && (probe < min || probe > max)
            ? ConversionResult.Failure(OverflowErrorCode)
            : ConversionResult.Failure(InvalidFormatErrorCode);
    }

    private static ConversionResult ConvertFloatingPoint(bool parsed, object result, bool isFinite)
    {
        if (!parsed)
        {
            return ConversionResult.Failure(InvalidFormatErrorCode);
        }

        // TryParse maps out-of-range literals to ±Infinity instead of failing.
        return isFinite ? ConversionResult.Success(result) : ConversionResult.Failure(OverflowErrorCode);
    }

    private static ConversionResult ConvertDecimal(string value)
    {
        string trimmed = value.Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
        {
            return ConversionResult.Success(result);
        }

        // A well-formed number decimal cannot hold → Overflow; otherwise garbage.
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double probe)
            && !double.IsNaN(probe)
            ? ConversionResult.Failure(OverflowErrorCode)
            : ConversionResult.Failure(InvalidFormatErrorCode);
    }

    private static ConversionResult ConvertTemporal<T>(
        string value,
        string? format,
        Func<string, string, (bool Ok, T Result)> parseExact,
        Func<string, IFormatProvider, (bool Ok, T Result)> parse)
        where T : struct
    {
        string trimmed = value.Trim();

        if (!string.IsNullOrEmpty(format))
        {
            (bool ok, T result) = parseExact(trimmed, format);
            return ok ? ConversionResult.Success(result) : ConversionResult.Failure(InvalidFormatErrorCode);
        }

        (bool invariantOk, T invariantResult) = parse(trimmed, CultureInfo.InvariantCulture);
        if (invariantOk)
        {
            return ConversionResult.Success(invariantResult);
        }

        (bool currentOk, T currentResult) = parse(trimmed, CultureInfo.CurrentCulture);
        return currentOk ? ConversionResult.Success(currentResult) : ConversionResult.Failure(InvalidFormatErrorCode);
    }

    private sealed record CompiledProperty(
        PropertyMapping Mapping,
        string ExpectedType,
        Func<string, ConversionResult> Converter,
        Action<TEntity, object?> Setter);

    private readonly record struct ConversionResult(object? Value, string? ErrorCode)
    {
        public static ConversionResult Success(object? value) => new(value, null);

        public static ConversionResult Failure(string errorCode) => new(null, errorCode);
    }
}
