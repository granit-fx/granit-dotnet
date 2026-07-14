namespace Granit.QueryEngine.Filtering.Exceptions;

/// <summary>
/// Thrown when a <see cref="QueryPredicate"/> fails strict validation against a query
/// definition. Unlike the lenient wire filter path (which silently drops unknown fields and
/// unconvertible values), the strict path collects and reports <b>every</b> violation so a
/// protocol adapter never silently returns a superset of the requested rows.
/// </summary>
public sealed class QueryPredicateValidationException : InvalidOperationException
{
    /// <summary>
    /// Initializes the exception with the full list of violations.
    /// </summary>
    /// <param name="errors">All violations found while validating the predicate tree.</param>
    public QueryPredicateValidationException(IReadOnlyList<QueryPredicateError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>All violations found while validating the predicate tree.</summary>
    public IReadOnlyList<QueryPredicateError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<QueryPredicateError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one error is required.", nameof(errors));
        }

        return "Query predicate validation failed with " +
            $"{errors.Count} error(s): " +
            string.Join("; ", errors.Select(e => e.Field is null ? $"[{e.Code}] {e.Message}" : $"[{e.Code}] {e.Field}: {e.Message}"));
    }
}
