namespace Granit.QueryEngine.Filtering;

/// <summary>
/// A single strict-validation violation found while validating a <see cref="QueryPredicate"/>
/// against a query definition. Carried by <see cref="Exceptions.QueryPredicateValidationException"/>.
/// </summary>
/// <param name="Field">The offending field name, or <c>null</c> for structural violations.</param>
/// <param name="Code">A stable machine-readable code from <see cref="QueryPredicateErrorCodes"/>.</param>
/// <param name="Message">A human-readable description of the violation.</param>
public sealed record QueryPredicateError(string? Field, string Code, string Message);

/// <summary>
/// Stable error codes for <see cref="QueryPredicateError.Code"/>.
/// </summary>
public static class QueryPredicateErrorCodes
{
    /// <summary>The field is not declared as a column on the query definition.</summary>
    public const string UnknownField = nameof(UnknownField);

    /// <summary>The field is a declared column but is not marked filterable.</summary>
    public const string FieldNotFilterable = nameof(FieldNotFilterable);

    /// <summary>The operator is not in the inferred whitelist for the field's CLR type.</summary>
    public const string OperatorNotAllowed = nameof(OperatorNotAllowed);

    /// <summary>The criterion value cannot be converted to the field's CLR type.</summary>
    public const string ValueNotConvertible = nameof(ValueNotConvertible);

    /// <summary>IsNull/IsNotNull was applied to a non-nullable value-type column.</summary>
    public const string NullCheckOnNonNullable = nameof(NullCheckOnNonNullable);

    /// <summary>The predicate tree exceeds <see cref="QueryPredicate.MaxDepth"/>.</summary>
    public const string TreeTooDeep = nameof(TreeTooDeep);

    /// <summary>The predicate tree exceeds <see cref="QueryPredicate.MaxLeafCount"/> leaf criteria.</summary>
    public const string TooManyCriteria = nameof(TooManyCriteria);
}
