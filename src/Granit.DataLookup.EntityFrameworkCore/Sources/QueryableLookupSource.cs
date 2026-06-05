using System.Linq.Expressions;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Sources;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataLookup.EntityFrameworkCore.Sources;

/// <summary>
/// Generic lookup source that wraps an <see cref="IQueryable{T}"/> and projects it to
/// the canonical <see cref="LookupItem"/> shape using caller-supplied value/label
/// selectors.
/// </summary>
/// <remarks>
/// <para>
/// The queryable is executed via <see cref="EntityFrameworkQueryableExtensions.ToListAsync{TSource}(IQueryable{TSource}, CancellationToken)"/>
/// so it benefits from EF Core's async materialization, tenant filters, and soft-delete
/// filters declared on the underlying <c>DbContext</c>.
/// </para>
/// <para>
/// For lookups that should reuse an existing <c>Granit.QueryEngine</c> definition (its global
/// search, sort, filters, and keyset/cursor pagination), prefer
/// <see cref="QueryDefinitionLookupSource{T}"/> over this lower-level primitive.
/// </para>
/// </remarks>
/// <typeparam name="T">The entity type exposed by this source.</typeparam>
public sealed class QueryableLookupSource<T> : ILookupSource
    where T : class
{
    private readonly Func<IQueryable<T>> _queryableFactory;
    private readonly Func<T, object> _valueSelector;
    private readonly Func<T, string> _labelSelector;
    private readonly Expression<Func<T, string>> _labelSortExpression;
    private readonly Expression<Func<T, string, bool>>? _searchPredicate;
    private readonly Expression<Func<T, object>> _valueEqualityExpression;
    private readonly IReadOnlyList<string> _scopeKeys;

    /// <summary>Initializes a new <see cref="QueryableLookupSource{T}"/>.</summary>
    /// <param name="name">Unique registry key.</param>
    /// <param name="queryableFactory">Factory that returns the base queryable (e.g. <c>() =&gt; dbContext.Set&lt;T&gt;()</c>).</param>
    /// <param name="valueSelector">Selector for <see cref="LookupItem.Value"/>. Also used to build the resolve-by-value filter.</param>
    /// <param name="labelSelector">Selector for <see cref="LookupItem.Label"/>, already culture-aware. Used to order results alphabetically.</param>
    /// <param name="searchPredicate">Optional predicate <c>(entity, searchTerm) =&gt; bool</c> used when a search term is provided.</param>
    /// <param name="requiredPermission">Optional permission the caller must hold.</param>
    /// <param name="scopeKeys">Scope keys the caller must supply with every search.</param>
    public QueryableLookupSource(
        string name,
        Func<IQueryable<T>> queryableFactory,
        Expression<Func<T, object>> valueSelector,
        Expression<Func<T, string>> labelSelector,
        Expression<Func<T, string, bool>>? searchPredicate = null,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(queryableFactory);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(labelSelector);

        Name = name;
        _queryableFactory = queryableFactory;
        _valueSelector = valueSelector.Compile();
        _labelSelector = labelSelector.Compile();
        _labelSortExpression = labelSelector;
        _searchPredicate = searchPredicate;
        _valueEqualityExpression = valueSelector;
        RequiredPermission = requiredPermission;
        _scopeKeys = scopeKeys ?? [];
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string? RequiredPermission { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> ScopeKeys => _scopeKeys;

    /// <inheritdoc/>
    public async ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<T> queryable = _queryableFactory();

        if (!string.IsNullOrWhiteSpace(query.Search) && _searchPredicate is not null)
        {
            queryable = queryable.Where(SubstituteSearchTerm(_searchPredicate, query.Search));
        }

        int pageSize = Math.Clamp(query.PageSize, 1, 200);
        int skip = Math.Max(0, (query.Page - 1) * pageSize);

        int totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        List<T> page = await queryable
            .OrderBy(_labelSortExpression)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        LookupItem[] items = [.. page.Select(e => new LookupItem(_valueSelector(e), _labelSelector(e)))];
        return new LookupResult(items, totalCount);
    }

    /// <inheritdoc/>
    public async ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        IQueryable<T> queryable = _queryableFactory();
        Expression<Func<T, bool>> predicate = BuildEqualityPredicate(value);

        T? entity = await queryable.FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        return new LookupItem(_valueSelector(entity), _labelSelector(entity));
    }

    private Expression<Func<T, bool>> BuildEqualityPredicate(object value)
    {
        // _valueEqualityExpression is `e => (object)e.Prop` — unwrap the Convert to compare against the native type.
        ParameterExpression parameter = _valueEqualityExpression.Parameters[0];
        Expression body = _valueEqualityExpression.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : _valueEqualityExpression.Body;

        object convertedValue = Convert.ChangeType(value, body.Type, System.Globalization.CultureInfo.InvariantCulture);
        BinaryExpression equality = Expression.Equal(body, Expression.Constant(convertedValue, body.Type));
        return Expression.Lambda<Func<T, bool>>(equality, parameter);
    }

    private static Expression<Func<T, bool>> SubstituteSearchTerm(
        Expression<Func<T, string, bool>> predicate,
        string searchTerm)
    {
        ParameterExpression entity = predicate.Parameters[0];
        Expression replaced = new SearchTermReplacer(predicate.Parameters[1], Expression.Constant(searchTerm))
            .Visit(predicate.Body);
        return Expression.Lambda<Func<T, bool>>(replaced, entity);
    }

    private sealed class SearchTermReplacer(ParameterExpression target, Expression replacement)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == target ? replacement : base.VisitParameter(node);
    }
}
