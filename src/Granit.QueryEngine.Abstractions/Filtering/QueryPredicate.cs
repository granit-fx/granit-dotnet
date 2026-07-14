namespace Granit.QueryEngine.Filtering;

/// <summary>
/// A programmatic, engine-validated boolean predicate tree over a query definition's
/// filterable columns — deliberately <b>NOT wire-bindable</b>.
/// </summary>
/// <remarks>
/// <para>
/// This closed tree (<see cref="FilterPredicate"/> / <see cref="AndPredicate"/> /
/// <see cref="OrPredicate"/> / <see cref="NotPredicate"/>) exists for protocol adapters
/// (e.g. OData) that must express boolean filter logic (<c>OR</c>, <c>NOT</c>, negated
/// equality, null checks) through the engine's single enforcement point instead of composing
/// arbitrary LINQ expressions around it.
/// </para>
/// <para>
/// It is deliberately not a property of <c>QueryRequest</c>: the wire surface stays a lenient
/// flat implicit-AND dictionary (UI-tolerant, unknown fields dropped), and accepting a
/// polymorphic JSON predicate tree from the network would be a new attack surface (unbounded
/// recursion, type discriminator abuse). Adapters construct the tree in code; the engine
/// validates it strictly and throws <see cref="Exceptions.QueryPredicateValidationException"/> listing
/// every violation.
/// </para>
/// <para>
/// Structural guards (<see cref="MaxDepth"/>, <see cref="MaxLeafCount"/>) are enforced at
/// validation time — when the engine builds the expression — not at construction.
/// </para>
/// </remarks>
public abstract record QueryPredicate
{
    /// <summary>Maximum nesting depth accepted at validation time.</summary>
    public const int MaxDepth = 32;

    /// <summary>Maximum number of leaf criteria accepted at validation time.</summary>
    public const int MaxLeafCount = 128;

    private protected QueryPredicate() { }

    /// <summary>
    /// Creates a leaf criterion. For <see cref="FilterOperator.IsNull"/> /
    /// <see cref="FilterOperator.IsNotNull"/> use the <see cref="Criterion(string, FilterOperator)"/>
    /// overload — those operators carry no value.
    /// </summary>
    /// <param name="field">The (case-insensitive) filterable column name.</param>
    /// <param name="op">The filter operator.</param>
    /// <param name="value">The filter value as a string.</param>
    public static QueryPredicate Criterion(string field, FilterOperator op, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(value);
        return new FilterPredicate(new FilterCriteria(field, op, value));
    }

    /// <summary>
    /// Creates a valueless leaf criterion (<see cref="FilterOperator.IsNull"/> /
    /// <see cref="FilterOperator.IsNotNull"/>). The leaf carries <c>Value = ""</c> by convention.
    /// </summary>
    /// <param name="field">The (case-insensitive) filterable column name.</param>
    /// <param name="op">The null-check operator.</param>
    public static QueryPredicate Criterion(string field, FilterOperator op) =>
        Criterion(field, op, string.Empty);

    /// <summary>Combines operands with AND semantics (all must match).</summary>
    /// <param name="operands">At least one operand.</param>
    public static QueryPredicate And(params ReadOnlySpan<QueryPredicate> operands) =>
        new AndPredicate(CopyOperands(operands));

    /// <summary>Combines operands with OR semantics (any may match).</summary>
    /// <param name="operands">At least one operand.</param>
    public static QueryPredicate Or(params ReadOnlySpan<QueryPredicate> operands) =>
        new OrPredicate(CopyOperands(operands));

    /// <summary>
    /// Negates a predicate. Negation follows C#/EF two-valued semantics: a NULL column
    /// MATCHES the negation of a match-style operator (e.g. <c>Not(Contains)</c> returns
    /// rows whose column is NULL).
    /// </summary>
    /// <param name="operand">The predicate to negate.</param>
    public static QueryPredicate Not(QueryPredicate operand)
    {
        ArgumentNullException.ThrowIfNull(operand);
        return new NotPredicate(operand);
    }

    private static QueryPredicate[] CopyOperands(ReadOnlySpan<QueryPredicate> operands)
    {
        if (operands.IsEmpty)
        {
            throw new ArgumentException("At least one operand is required.", nameof(operands));
        }

        QueryPredicate[] copy = operands.ToArray();
        foreach (QueryPredicate operand in copy)
        {
            ArgumentNullException.ThrowIfNull(operand, nameof(operands));
        }

        return copy;
    }
}

/// <summary>
/// Leaf predicate: a single <see cref="FilterCriteria"/> evaluated against a filterable column.
/// </summary>
/// <param name="Criteria">The filter criterion (reuses the wire-filter leaf shape).</param>
public sealed record FilterPredicate(FilterCriteria Criteria) : QueryPredicate;

/// <summary>
/// Conjunction: all <paramref name="Operands"/> must match.
/// </summary>
/// <param name="Operands">The operands (AND-combined).</param>
public sealed record AndPredicate(IReadOnlyList<QueryPredicate> Operands) : QueryPredicate;

/// <summary>
/// Disjunction: at least one of <paramref name="Operands"/> must match.
/// </summary>
/// <param name="Operands">The operands (OR-combined).</param>
public sealed record OrPredicate(IReadOnlyList<QueryPredicate> Operands) : QueryPredicate;

/// <summary>
/// Negation of <paramref name="Operand"/>, following C#/EF two-valued semantics
/// (a NULL column matches the negation of a match-style operator).
/// </summary>
/// <param name="Operand">The negated predicate.</param>
public sealed record NotPredicate(QueryPredicate Operand) : QueryPredicate;
