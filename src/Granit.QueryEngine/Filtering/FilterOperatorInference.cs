namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Infers available filter operators from CLR types (HotChocolate-style type inference).
/// </summary>
public static class FilterOperatorInference
{
    private static readonly IReadOnlyList<FilterOperator> StringOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Contains,
        FilterOperator.StartsWith,
        FilterOperator.EndsWith,
        FilterOperator.In,
    ];

    private static readonly IReadOnlyList<FilterOperator> NumericOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Gt,
        FilterOperator.Gte,
        FilterOperator.Lt,
        FilterOperator.Lte,
        FilterOperator.Between,
        FilterOperator.In,
    ];

    private static readonly IReadOnlyList<FilterOperator> DateOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Gt,
        FilterOperator.Gte,
        FilterOperator.Lt,
        FilterOperator.Lte,
        FilterOperator.Between,
    ];

    private static readonly IReadOnlyList<FilterOperator> BoolOperators =
    [
        FilterOperator.Eq,
    ];

    private static readonly IReadOnlyList<FilterOperator> EnumOperators =
    [
        FilterOperator.Eq,
        FilterOperator.In,
    ];

    private static readonly IReadOnlyList<FilterOperator> GuidOperators =
    [
        FilterOperator.Eq,
        FilterOperator.In,
    ];

    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(byte),
        typeof(decimal),
        typeof(double),
        typeof(float),
    ];

    private static readonly HashSet<Type> DateTypes =
    [
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
    ];

    /// <summary>
    /// Returns the available filter operators for the given CLR type.
    /// Nullable types are unwrapped to their underlying type.
    /// </summary>
    /// <param name="clrType">The CLR type of the property.</param>
    /// <returns>The list of supported operators, or an empty list for unsupported types.</returns>
    public static IReadOnlyList<FilterOperator> GetOperators(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        Type underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (underlying == typeof(string))
        {
            return StringOperators;
        }

        if (NumericTypes.Contains(underlying))
        {
            return NumericOperators;
        }

        if (DateTypes.Contains(underlying))
        {
            return DateOperators;
        }

        if (underlying == typeof(bool))
        {
            return BoolOperators;
        }

        if (underlying.IsEnum)
        {
            return EnumOperators;
        }

        if (underlying == typeof(Guid))
        {
            return GuidOperators;
        }

        return [];
    }
}
