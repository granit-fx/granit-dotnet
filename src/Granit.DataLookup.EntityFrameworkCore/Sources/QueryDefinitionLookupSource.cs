using System.Globalization;
using System.Linq.Expressions;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataLookup.EntityFrameworkCore.Sources;

/// <summary>
/// Lookup source backed by a <see cref="QueryDefinition{TEntity}"/> that declared
/// <see cref="QueryDefinitionBuilder{TEntity}.AsLookup{TValue}"/>. Delegates search, sort,
/// pagination, and keyset/cursor handling to <see cref="IQueryEngine{TEntity}"/>, then
/// projects the materialized page to the canonical <see cref="LookupItem"/> shape.
/// </summary>
/// <remarks>
/// <para>
/// The non-projecting <see cref="IQueryEngine{TEntity}.ExecuteAsync(IQueryable{TEntity}, QueryRequest, CancellationToken)"/>
/// overload is used on purpose: it is the only engine path that returns a <c>NextCursor</c>,
/// so the lookup transparently supports infinite-scroll for definitions that opt into cursor
/// pagination (<c>SupportsCursorPagination</c>). Only the requested page of entities is
/// materialized (default 25 rows) and then projected in-memory — negligible over a typeahead
/// page, and it keeps cursor support that server-side projection would forfeit.
/// </para>
/// <para>
/// Scope keys are mapped to equality filters on the matching filterable columns, so a scoped
/// lookup (e.g. <c>meter-definitions</c> scoped by <c>tenantId</c>) filters server-side via the
/// definition's own filter pipeline.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The entity type exposed by the definition.</typeparam>
public sealed class QueryDefinitionLookupSource<TEntity> : ILookupSource, IKindProviderLookupSource
    where TEntity : class
{
    private readonly IQueryEngine<TEntity> _engine;
    private readonly Func<IQueryable<TEntity>> _queryableFactory;
    private readonly Func<TEntity, object> _valueSelector;
    private readonly Func<TEntity, string> _labelSelector;
    private readonly LambdaExpression _valueExpression;
    private readonly Type _valueType;
    private readonly string[] _filterableColumns;

    /// <summary>Initializes a new <see cref="QueryDefinitionLookupSource{TEntity}"/>.</summary>
    /// <param name="engine">The query engine bound to <typeparamref name="TEntity"/>.</param>
    /// <param name="definition">The query definition; MUST declare <c>AsLookup</c>.</param>
    /// <param name="queryableFactory">Factory returning the base queryable (e.g. <c>() =&gt; db.Set&lt;TEntity&gt;()</c>).</param>
    /// <exception cref="InvalidOperationException">Thrown when the definition did not declare <c>AsLookup</c>.</exception>
    public QueryDefinitionLookupSource(
        IQueryEngine<TEntity> engine,
        QueryDefinition<TEntity> definition,
        Func<IQueryable<TEntity>> queryableFactory)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(queryableFactory);

        LookupSourceDescriptor descriptor = definition.GetLookupSource()
            ?? throw new InvalidOperationException(
                $"Query definition '{definition.Name}' does not declare a lookup source. Call " +
                "builder.AsLookup(...) in its Configure method before registering it with AddQueryDefinitionLookup.");

        _engine = engine;
        _queryableFactory = queryableFactory;
        _valueSelector = ((Expression<Func<TEntity, object>>)descriptor.BoxedValueSelector).Compile();
        _labelSelector = ((Expression<Func<TEntity, string>>)descriptor.LabelSelector).Compile();
        _valueExpression = descriptor.ValueSelector;
        _valueType = descriptor.ValueType;
        Name = descriptor.Name;
        RequiredPermission = descriptor.RequiredPermission;
        ScopeKeys = descriptor.ScopeKeys;
        _filterableColumns = [.. definition.GetColumns().Where(c => c.IsFilterable).Select(c => c.PropertyName)];
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string? RequiredPermission { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> ScopeKeys { get; }

    LookupKind IKindProviderLookupSource.Kind => LookupKind.QueryEngine;

    /// <inheritdoc/>
    public async ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        QueryRequest request = new()
        {
            Search = query.Search,
            PageSize = query.PageSize,
            Cursor = query.ContinuationToken,
            Page = query.ContinuationToken is null ? query.Page : null,
            Filter = BuildScopeFilter(query.Scope),
        };

        PagedResult<TEntity> result = await _engine
            .ExecuteAsync(_queryableFactory(), request, cancellationToken)
            .ConfigureAwait(false);

        LookupItem[] items = [.. result.Items.Select(e => new LookupItem(_valueSelector(e), _labelSelector(e)))];
        return new LookupResult(items, result.TotalCount, result.NextCursor);
    }

    /// <inheritdoc/>
    public async ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        Expression<Func<TEntity, bool>> predicate = BuildEqualityPredicate(value);

        TEntity? entity = await _queryableFactory()
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : new LookupItem(_valueSelector(entity), _labelSelector(entity));
    }

    private Dictionary<string, string>? BuildScopeFilter(IReadOnlyDictionary<string, string?>? scope)
    {
        if (scope is null || scope.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> filter = new(StringComparer.Ordinal);
        foreach ((string key, string? value) in scope)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string? column = Array.Find(_filterableColumns, c => string.Equals(c, key, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
            {
                filter[$"{column}.eq"] = value;
            }
        }

        return filter.Count == 0 ? null : filter;
    }

    private Expression<Func<TEntity, bool>> BuildEqualityPredicate(object value)
    {
        ParameterExpression parameter = _valueExpression.Parameters[0];
        Expression body = _valueExpression.Body;
        object converted = ConvertToValueType(value);
        BinaryExpression equality = Expression.Equal(body, Expression.Constant(converted, body.Type));
        return Expression.Lambda<Func<TEntity, bool>>(equality, parameter);
    }

    private object ConvertToValueType(object value)
    {
        if (_valueType.IsInstanceOfType(value))
        {
            return value;
        }

        string text = value.ToString()
            ?? throw new InvalidOperationException("Lookup value cannot be converted to a string.");

        if (_valueType == typeof(Guid))
        {
            return Guid.Parse(text);
        }

        if (_valueType == typeof(string))
        {
            return text;
        }

        if (_valueType.IsEnum)
        {
            return Enum.Parse(_valueType, text, ignoreCase: true);
        }

        return Convert.ChangeType(text, _valueType, CultureInfo.InvariantCulture);
    }
}
