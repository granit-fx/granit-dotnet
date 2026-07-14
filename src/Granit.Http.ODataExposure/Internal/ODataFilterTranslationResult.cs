using Granit.QueryEngine.Filtering;

namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Outcome of translating an OData <c>$filter</c> AST into a
/// <see cref="QueryPredicate"/> tree. A <see langword="null"/>
/// <see cref="RejectionDetail"/> means the translation succeeded and
/// <see cref="Predicate"/> carries the engine-consumable predicate; otherwise
/// <see cref="RejectionDetail"/> is the actionable message returned to the BI
/// client in the 400 Problem body and <see cref="RejectedNodeKind"/> names the
/// offending AST node kind for diagnostics.
/// </summary>
internal sealed record ODataFilterTranslationResult
{
    /// <summary>The translated predicate tree; non-null exactly when <see cref="RejectionDetail"/> is null.</summary>
    public QueryPredicate? Predicate { get; init; }

    /// <summary>Actionable rejection message for the 400 Problem body, or <see langword="null"/> on success.</summary>
    public string? RejectionDetail { get; init; }

    /// <summary>Name of the OData AST node kind that caused the rejection, or <see langword="null"/> on success.</summary>
    public string? RejectedNodeKind { get; init; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="predicate">The translated predicate tree.</param>
    public static ODataFilterTranslationResult Success(QueryPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new() { Predicate = predicate };
    }

    /// <summary>Creates a rejection result.</summary>
    /// <param name="nodeKind">The offending OData AST node kind.</param>
    /// <param name="detail">Actionable message returned to the client.</param>
    public static ODataFilterTranslationResult Rejected(string nodeKind, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new() { RejectedNodeKind = nodeKind, RejectionDetail = detail };
    }
}
