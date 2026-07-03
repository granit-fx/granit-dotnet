using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Extension methods for applying pagination to an <see cref="IQueryable{T}"/>.
/// </summary>
internal static class QueryablePaginationExtensions
{
    /// <summary>
    /// Applies offset pagination (page/pageSize) and returns a <see cref="PagedResult{T}"/>.
    /// When <paramref name="precomputedCount"/> is provided, skips the <c>COUNT(*)</c> query.
    /// When <c>null</c>, fetches <c>pageSize + 1</c> to determine <c>HasMore</c> without counting.
    /// </summary>
    public static async Task<PagedResult<T>> ApplyOffsetPaginationAsync<T>(
        this IQueryable<T> source,
        int page,
        int pageSize,
        int? precomputedCount,
        CancellationToken cancellationToken)
    {
        int skip = (page - 1) * pageSize;

        if (precomputedCount is null)
        {
            // Fetch pageSize + 1 to determine HasMore without COUNT(*)
            List<T> items = await source
                .Skip(skip)
                .Take(pageSize + 1)
                .ToListSafeAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = items.Count > pageSize;
            if (hasMore)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new PagedResult<T>(items, TotalCount: null, HasMore: hasMore);
        }

        int totalCount = precomputedCount.Value;

        List<T> pagedItems = await source
            .Skip(skip)
            .Take(pageSize)
            .ToListSafeAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMorePages = skip + pagedItems.Count < totalCount;
        return new PagedResult<T>(pagedItems, totalCount, HasMore: hasMorePages);
    }

    /// <summary>
    /// Applies keyset/cursor pagination and returns a <see cref="PagedResult{T}"/>.
    /// When <paramref name="effectiveSort"/> is provided, uses composite cursor encoding
    /// (all sort field values in the cursor) for correct keyset semantics with dynamic sorting.
    /// Falls back to legacy single-field cursor for backward compatibility.
    /// </summary>
    public static async Task<PagedResult<T>> ApplyCursorPaginationAsync<T>(
        this IQueryable<T> source,
        string? cursor,
        int pageSize,
        string cursorPropertyName,
        CancellationToken cancellationToken,
        ILogger? logger = null,
        string? effectiveSort = null,
        IReadOnlySet<string>? sortableFields = null,
        byte[]? cursorHmacKey = null)
        where T : class
    {
        PropertyInfo? cursorProperty = typeof(T).GetProperty(
            cursorPropertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (cursorProperty is null)
        {
            List<T> fallback = await source.Take(pageSize).ToListSafeAsync(cancellationToken).ConfigureAwait(false);
            return new PagedResult<T>(fallback, TotalCount: null, HasMore: false);
        }

        IQueryable<T> query = source;

        // Parse sort fields for composite cursor support
        List<CompositeCursorBuilder.SortField> sortFields = [];
        if (!string.IsNullOrWhiteSpace(effectiveSort))
        {
            sortFields = CompositeCursorBuilder.ParseSortFields<T>(effectiveSort, sortableFields);

            // Ensure cursor property is included as tiebreaker (append if missing)
            if (!sortFields.Exists(f => f.Path.Equals(cursorPropertyName, StringComparison.OrdinalIgnoreCase)))
            {
                sortFields.Add(new CompositeCursorBuilder.SortField(cursorProperty.Name, cursorProperty, Descending: false));
            }
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            query = ApplyCursorFilter(query, cursor, cursorProperty, cursorPropertyName, sortFields, logger, cursorHmacKey);
        }

        // Take pageSize + 1 to determine if there are more pages
        List<T> items = await query
            .Take(pageSize + 1)
            .ToListSafeAsync(cancellationToken)
            .ConfigureAwait(false);

        string? nextCursor = null;
        bool hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = EncodeCursor(items[^1], cursorProperty, sortFields, cursorHmacKey);
        }

        return new PagedResult<T>(items, TotalCount: null, HasMore: hasMore, NextCursor: nextCursor);
    }

    private static IQueryable<T> ApplyCursorFilter<T>(
        IQueryable<T> query,
        string cursor,
        PropertyInfo cursorProperty,
        string cursorPropertyName,
        List<CompositeCursorBuilder.SortField> sortFields,
        ILogger? logger,
        byte[]? hmacKey = null)
        where T : class
    {
        Dictionary<string, string>? compositeValues = CursorEncoder.DecodeComposite(cursor, logger, hmacKey);

        if (compositeValues is not null && sortFields.Count > 0)
        {
            Expression<Func<T, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<T>(
                sortFields, compositeValues, logger);

            return predicate is not null ? query.Where(predicate) : query;
        }

        return ApplyLegacyCursorFilter(query, cursor, cursorProperty, cursorPropertyName, logger);
    }

    private static IQueryable<T> ApplyLegacyCursorFilter<T>(
        IQueryable<T> query,
        string cursor,
        PropertyInfo cursorProperty,
        string cursorPropertyName,
        ILogger? logger)
        where T : class
    {
        string? cursorValue = CursorEncoder.Decode<string>(cursor, logger);
        if (cursorValue is null)
        {
            return query;
        }

        object? converted = FilterExpressionBuilder.ConvertValue(
            cursorValue,
            Nullable.GetUnderlyingType(cursorProperty.PropertyType) ?? cursorProperty.PropertyType,
            logger, cursorPropertyName);

        if (converted is null)
        {
            return query;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        MemberExpression member = Expression.Property(parameter, cursorProperty);
        ConstantExpression constant = Expression.Constant(converted, cursorProperty.PropertyType);
        BinaryExpression greaterThan = Expression.GreaterThan(member, constant);
        var predicate = Expression.Lambda<Func<T, bool>>(greaterThan, parameter);
        return query.Where(predicate);
    }

    private static string? EncodeCursor<T>(
        T lastItem,
        PropertyInfo cursorProperty,
        List<CompositeCursorBuilder.SortField> sortFields,
        byte[]? hmacKey = null)
        where T : class
    {
        if (sortFields.Count > 0)
        {
            return CompositeCursorBuilder.EncodeCompositeCursor(lastItem, sortFields, hmacKey);
        }

        object? lastValue = cursorProperty.GetValue(lastItem);
        return lastValue is not null ? CursorEncoder.Encode(lastValue.ToString()!, hmacKey) : null;
    }
}
