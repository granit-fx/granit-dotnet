using System.Linq.Expressions;

namespace Granit.QueryEngine;

/// <summary>
/// Declares that a <see cref="QueryDefinition{TEntity}"/> also serves as a data-lookup
/// source (typeahead picker) registered under <see cref="Name"/>. Produced by
/// <see cref="QueryDefinitionBuilder{TEntity}.AsLookup{TValue}"/> and consumed by
/// <c>Granit.DataLookup.EntityFrameworkCore</c>'s <c>QueryDefinitionLookupSource&lt;TEntity&gt;</c>,
/// which reuses the query engine's search, sort, and keyset-pagination pipeline.
/// </summary>
public sealed record LookupSourceDescriptor
{
    /// <summary>Unique lookup registry key (e.g. <c>"tenants"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Selector for the lookup value, typed as declared (<c>Func&lt;TEntity, TValue&gt;</c>).
    /// Used to build the resolve-by-value equality predicate.
    /// </summary>
    public required LambdaExpression ValueSelector { get; init; }

    /// <summary>
    /// Selector for the lookup value boxed to <see cref="object"/>
    /// (<c>Func&lt;TEntity, object&gt;</c>). Used to project a materialized row to
    /// <c>LookupItem.Value</c>.
    /// </summary>
    public required LambdaExpression BoxedValueSelector { get; init; }

    /// <summary>Selector for the display label (<c>Func&lt;TEntity, string&gt;</c>).</summary>
    public required LambdaExpression LabelSelector { get; init; }

    /// <summary>The CLR type of the lookup value, used for resolve-by-value conversion.</summary>
    public required Type ValueType { get; init; }

    /// <summary>Optional permission the caller must hold to invoke the lookup.</summary>
    public string? RequiredPermission { get; init; }

    /// <summary>
    /// Scope keys the caller must supply with every search. Each key SHOULD match a
    /// filterable column on the definition so its value is applied as an equality filter
    /// (the mechanism behind cascading pickers).
    /// </summary>
    public IReadOnlyList<string> ScopeKeys { get; init; } = [];
}
