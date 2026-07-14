namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Infers available filter operators from CLR types (HotChocolate-style type inference).
/// </summary>
public static class FilterOperatorInference
{
    private static readonly IReadOnlyList<FilterOperator> StringOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Ne,
        FilterOperator.Contains,
        FilterOperator.StartsWith,
        FilterOperator.EndsWith,
        FilterOperator.In,
    ];

    private static readonly IReadOnlyList<FilterOperator> NumericOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Ne,
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
        FilterOperator.Ne,
        FilterOperator.Gt,
        FilterOperator.Gte,
        FilterOperator.Lt,
        FilterOperator.Lte,
        FilterOperator.Between,
    ];

    private static readonly IReadOnlyList<FilterOperator> BoolOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Ne,
    ];

    private static readonly IReadOnlyList<FilterOperator> EnumOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Ne,
        FilterOperator.In,
    ];

    private static readonly IReadOnlyList<FilterOperator> GuidOperators =
    [
        FilterOperator.Eq,
        FilterOperator.Ne,
        FilterOperator.In,
    ];

    private static readonly IReadOnlyList<FilterOperator> NullCheckOperators =
    [
        FilterOperator.IsNull,
        FilterOperator.IsNotNull,
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

    /// <summary>
    /// Returns the available filter operators for the given CLR type, additionally offering
    /// <see cref="FilterOperator.IsNull"/> / <see cref="FilterOperator.IsNotNull"/> when the
    /// column is nullable. The base operator set is identical to <see cref="GetOperators(Type)"/>.
    /// </summary>
    /// <param name="clrType">The CLR type of the property.</param>
    /// <param name="isNullableColumn">
    /// Whether the column can hold NULL. Use <see cref="IsNullableColumnType(Type)"/> when only
    /// the CLR type is known (nullable reference annotations are erased at runtime, so a
    /// reference-typed column is treated as nullable).
    /// </param>
    /// <returns>The list of supported operators, or an empty list for unsupported types.</returns>
    public static IReadOnlyList<FilterOperator> GetOperators(Type clrType, bool isNullableColumn)
    {
        IReadOnlyList<FilterOperator> baseOperators = GetOperators(clrType);

        if (!isNullableColumn || baseOperators.Count == 0)
        {
            return baseOperators;
        }

        return [.. baseOperators, .. NullCheckOperators];
    }

    /// <summary>
    /// Determines whether a column of the given CLR type can hold NULL: reference types and
    /// <see cref="Nullable{T}"/> value types. Nullable reference annotations (<c>string?</c> vs
    /// <c>string</c>) are erased at runtime, so every reference type is treated as nullable.
    /// </summary>
    /// <param name="clrType">The CLR type of the property.</param>
    public static bool IsNullableColumnType(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return !clrType.IsValueType || Nullable.GetUnderlyingType(clrType) is not null;
    }
}
