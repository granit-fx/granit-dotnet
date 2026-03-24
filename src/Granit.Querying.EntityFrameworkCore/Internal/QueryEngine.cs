using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Granit.MultiTenancy;
using Granit.Querying.EntityFrameworkCore.Diagnostics;
using Granit.Querying.Filtering;
using Granit.Querying.Meta;
using Granit.Querying.SavedViews;
using Granit.Querying.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Querying.EntityFrameworkCore.Internal;

/// <summary>
/// Default implementation of <see cref="IQueryEngine{TEntity}"/>.
/// Pipeline: parse filters → whitelist validate → apply filters → apply presets
/// → apply global search → apply sort → count → apply pagination (or group by).
/// </summary>
internal sealed class QueryEngine<TEntity>(
    QueryDefinition<TEntity> definition,
    ILogger<QueryEngine<TEntity>> logger,
    IGlobalSearchStrategy<TEntity>? searchStrategy = null,
    QueryingEfCoreMetrics? metrics = null,
    ICurrentTenant? currentTenant = null) : IQueryEngine<TEntity>
    where TEntity : class
{
    private static readonly string EntityTypeName = typeof(TEntity).Name;

    private readonly QueryDefinitionBuilder<TEntity> _builder = definition.GetBuilder();
    private readonly ILogger _logger = logger;
    private readonly IGlobalSearchStrategy<TEntity> _searchStrategy = searchStrategy ?? new ContainsSearchStrategy<TEntity>();
    private readonly QueryingEfCoreMetrics? _metrics = metrics;
    private readonly ICurrentTenant? _currentTenant = currentTenant;

    /// <inheritdoc/>
    public async Task<PagedResult<TEntity>> ExecuteAsync(
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = QueryingEfCoreActivitySource.Source.StartActivity(QueryingEfCoreActivitySource.ExecuteQuery);
        activity?.SetTag("entity_type", EntityTypeName);
        long startTimestamp = Stopwatch.GetTimestamp();

        IQueryable<TEntity> filtered = ApplyCommonFilters(source.AsNoTracking(), request);

        // Pagination
        int pageSize = ClampPageSize(request.PageSize);

        PagedResult<TEntity> result;

        if (request.Cursor is not null && _builder.CursorPropertyName is not null)
        {
            // Cursor pagination: sort first, then apply cursor filter
            string effectiveSort = string.IsNullOrWhiteSpace(request.Sort)
                ? _builder.DefaultSortValue ?? string.Empty
                : request.Sort;
            IQueryable<TEntity> sorted = filtered.ApplySort(request.Sort, _builder);
            result = await sorted.ApplyCursorPaginationAsync(
                request.Cursor, pageSize, _builder.CursorPropertyName, cancellationToken,
                _logger, effectiveSort)
                .ConfigureAwait(false);
        }
        else
        {
            // Offset pagination: count on filtered (unsorted), then sort + paginate
            int page = request.Page ?? 1;
            if (page < 1)
            {
                page = 1;
            }

            int? precomputedCount = request.SkipTotalCount
                ? null
                : await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

            IQueryable<TEntity> query = filtered.ApplySort(request.Sort, _builder);

            result = await query.ApplyOffsetPaginationAsync(page, pageSize, precomputedCount, cancellationToken)
                .ConfigureAwait(false);
        }

        RecordMetrics("paged", startTimestamp);
        return result;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<TProjection>> ExecuteAsync<TProjection>(
        IQueryable<TEntity> source,
        QueryRequest request,
        Expression<Func<TEntity, TProjection>> projection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        IQueryable<TEntity> filtered = ApplyCommonFilters(source.AsNoTracking(), request);

        int pageSize = ClampPageSize(request.PageSize);

        if (request.Cursor is not null && _builder.CursorPropertyName is not null)
        {
            // Cursor pagination with projection: sort, apply cursor, project, materialize
            IQueryable<TEntity> sorted = filtered.ApplySort(request.Sort, _builder);
            return await ApplyCursorPaginationWithProjectionAsync(
                sorted, request.Cursor, pageSize, projection, cancellationToken)
                .ConfigureAwait(false);
        }

        // Offset pagination with projection
        int page = request.Page ?? 1;
        if (page < 1)
        {
            page = 1;
        }

        int? precomputedCount = request.SkipTotalCount
            ? null
            : await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<TEntity> sorted2 = filtered.ApplySort(request.Sort, _builder);
        int skip = (page - 1) * pageSize;

        if (precomputedCount is null)
        {
            // Fetch pageSize + 1 to determine HasMore without COUNT(*)
            List<TProjection> items = await sorted2
                .Skip(skip)
                .Take(pageSize + 1)
                .Select(projection)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = items.Count > pageSize;
            if (hasMore)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new PagedResult<TProjection>(items, TotalCount: null, HasMore: hasMore);
        }

        List<TProjection> pagedItems = await sorted2
            .Skip(skip)
            .Take(pageSize)
            .Select(projection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMorePages = skip + pagedItems.Count < precomputedCount.Value;
        return new PagedResult<TProjection>(pagedItems, precomputedCount.Value, HasMore: hasMorePages);
    }

    /// <inheritdoc/>
    public async Task<GroupedResult<TEntity>> ExecuteGroupedAsync(
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = QueryingEfCoreActivitySource.Source.StartActivity(QueryingEfCoreActivitySource.ExecuteGrouped);
        activity?.SetTag("entity_type", EntityTypeName);
        long startTimestamp = Stopwatch.GetTimestamp();

        if (string.IsNullOrWhiteSpace(request.GroupBy))
        {
            return new GroupedResult<TEntity>([], 0);
        }

        IQueryable<TEntity> query = ApplyCommonFilters(source.AsNoTracking(), request);

        GroupedResult<TEntity> result = await query.ApplyGroupByAsync(request.GroupBy, _builder, cancellationToken)
            .ConfigureAwait(false);

        RecordMetrics("grouped", startTimestamp);
        return result;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TEntity> ExecuteStreamAsync(
        IQueryable<TEntity> source,
        QueryRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Activity? activity = QueryingEfCoreActivitySource.Source.StartActivity(QueryingEfCoreActivitySource.ExecuteStream);
        activity?.SetTag("entity_type", EntityTypeName);
        long startTimestamp = Stopwatch.GetTimestamp();

        IQueryable<TEntity> query = ApplyCommonFilters(source.AsNoTracking(), request);
        query = query.ApplySort(request.Sort, _builder);

        int limit = _builder.MaxStreamSizeValue;
        int count = 0;

        await foreach (TEntity entity in query.Take(limit).AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
            yield return entity;
        }

        if (count >= limit)
        {
            QueryingEfCoreLog.StreamLimitReached(_logger, EntityTypeName, limit);
            _metrics?.RecordStreamLimitReached(GetTenantId(), EntityTypeName);
        }

        RecordMetrics("stream", startTimestamp);
        activity?.Dispose();
    }

    /// <inheritdoc/>
    public QueryMetadata GetMetadata(IReadOnlyList<SavedViewSummary>? savedViews = null) =>
        new()
        {
            Columns = _builder.Columns.Select(c => new ColumnDefinition(
                c.PropertyName,
                c.Label ?? c.PropertyName,
                c.ClrType.Name,
                c.Order,
                c.IsSortable,
                c.IsFilterable,
                c.IsVisible,
                c.Format)).ToList(),
            FilterableFields = _builder.Columns
                .Where(c => c.IsFilterable)
                .Select(c => new FilterableField(
                    c.PropertyName,
                    c.ClrType.Name,
                    FilterOperatorInference.GetOperators(c.ClrType)))
                .ToList(),
            SortableFields = _builder.Columns
                .Where(c => c.IsSortable)
                .Select(c => new SortableField(c.PropertyName))
                .ToList(),
            PresetFilterGroups = _builder.FilterGroups.Select(g => new FilterGroupMeta(
                g.Name,
                g.Label ?? g.Name,
                g.Presets.Select(p => new PresetMeta(
                    p.Name,
                    p.Label ?? p.Name,
                    p.IsDefault)).ToList())).ToList(),
            QuickFilters = _builder.QuickFilters.Select(f => new QuickFilterMeta(
                f.Name,
                f.Label ?? f.Name,
                f.IsDefault)).ToList(),
            DateFilters = _builder.DateFilters.Select(d => new DateFilterMeta(
                d.PropertyName,
                d.DefaultPeriod,
                Enum.GetValues<DatePeriod>().ToList())).ToList(),
            GroupByFields = _builder.GroupByFields.Select(g => new GroupByField(
                g.PropertyName,
                g.ClrType.Name)).ToList(),
            Pagination = new PaginationMeta(
                _builder.DefaultPageSizeValue,
                _builder.MaxPageSizeValue,
                _builder.MaxStreamSizeValue,
                _builder.CursorPropertyName is not null),
            DefaultSort = _builder.DefaultSortValue,
        };

    private async Task<PagedResult<TProjection>> ApplyCursorPaginationWithProjectionAsync<TProjection>(
        IQueryable<TEntity> sortedSource,
        string cursor,
        int pageSize,
        Expression<Func<TEntity, TProjection>> projection,
        CancellationToken cancellationToken)
    {
        // Apply cursor filter on entity query, then project
        // We reuse the cursor pagination logic but inline it here to project before materializing
        string cursorPropertyName = _builder.CursorPropertyName!;
        System.Reflection.PropertyInfo? property = typeof(TEntity).GetProperty(
            cursorPropertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

        IQueryable<TEntity> query = sortedSource;

        if (property is not null && !string.IsNullOrEmpty(cursor))
        {
            string? cursorValue = CursorEncoder.Decode<string>(cursor, _logger);
            if (cursorValue is not null)
            {
                object? converted = FilterExpressionBuilder.ConvertValue(
                    cursorValue,
                    Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType,
                    _logger, cursorPropertyName);

                if (converted is not null)
                {
                    ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
                    MemberExpression member = Expression.Property(parameter, property);
                    ConstantExpression constant = Expression.Constant(converted, property.PropertyType);
                    BinaryExpression greaterThan = Expression.GreaterThan(member, constant);
                    var predicate = Expression.Lambda<Func<TEntity, bool>>(greaterThan, parameter);
                    query = query.Where(predicate);
                }
            }
        }

        List<TProjection> items = await query
            .Take(pageSize + 1)
            .Select(projection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        // Note: NextCursor cannot be computed from projected items (we don't know the cursor property value)
        // Cursor pagination with projection does not support NextCursor
        return new PagedResult<TProjection>(items, TotalCount: null, HasMore: hasMore);
    }

    private IQueryable<TEntity> ApplyCommonFilters(IQueryable<TEntity> source, QueryRequest request)
    {
        IQueryable<TEntity> query = source;

        // Parse and apply filters
        if (request.Filter is not null)
        {
            List<FilterCriteria> criteria = ParseFilterCriteria(request.Filter);
            query = query.ApplyFilters(criteria, _builder, _logger);
        }

        // Apply presets
        query = query.ApplyPresets(request.Presets, _builder);

        // Apply quick filters
        query = query.ApplyQuickFilters(request.QuickFilters, _builder);

        // Apply global search via strategy (default: ContainsSearchStrategy = LIKE '%term%')
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = _searchStrategy.ApplySearch(query, request.Search, _builder.GlobalSearchProperties);
        }

        return query;
    }

    private int ClampPageSize(int? requestedSize)
    {
        if (requestedSize is null || requestedSize <= 0)
        {
            return _builder.DefaultPageSizeValue;
        }

        return Math.Min(requestedSize.Value, _builder.MaxPageSizeValue);
    }

    private static List<FilterCriteria> ParseFilterCriteria(IReadOnlyDictionary<string, string> filter)
    {
        List<FilterCriteria> criteria = [];

        foreach ((string key, string value) in filter)
        {
            int dotIndex = key.LastIndexOf('.');
            if (dotIndex <= 0 || dotIndex >= key.Length - 1)
            {
                continue;
            }

            string field = key[..dotIndex];
            string operatorStr = key[(dotIndex + 1)..];

            if (Enum.TryParse<FilterOperator>(operatorStr, ignoreCase: true, out FilterOperator op))
            {
                criteria.Add(new FilterCriteria(field, op, value));
            }
        }

        return criteria;
    }

    private string? GetTenantId() =>
        _currentTenant is { IsAvailable: true } ? _currentTenant.Id?.ToString() : null;

    private void RecordMetrics(string mode, long startTimestamp)
    {
        if (_metrics is null)
        {
            return;
        }

        string? tenantId = GetTenantId();
        double elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        _metrics.RecordQueryExecuted(tenantId, EntityTypeName, mode);
        _metrics.RecordQueryDuration(tenantId, EntityTypeName, mode, elapsed);
    }
}
