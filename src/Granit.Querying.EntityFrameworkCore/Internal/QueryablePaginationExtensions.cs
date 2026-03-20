using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Querying.EntityFrameworkCore.Internal;

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
                .ToListAsync(cancellationToken)
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
            .ToListAsync(cancellationToken)
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
        string? effectiveSort = null)
        where T : class
    {
        PropertyInfo? cursorProperty = typeof(T).GetProperty(
            cursorPropertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (cursorProperty is null)
        {
            List<T> fallback = await source.Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
            return new PagedResult<T>(fallback, TotalCount: null, HasMore: false);
        }

        IQueryable<T> query = source;

        // Parse sort fields for composite cursor support
        List<CompositeCursorBuilder.SortField> sortFields = [];
        if (!string.IsNullOrWhiteSpace(effectiveSort))
        {
            sortFields = CompositeCursorBuilder.ParseSortFields<T>(effectiveSort);

            // Ensure cursor property is included as tiebreaker (append if missing)
            if (!sortFields.Exists(f => f.Property.Name.Equals(cursorPropertyName, StringComparison.OrdinalIgnoreCase)))
            {
                sortFields.Add(new CompositeCursorBuilder.SortField(cursorProperty, Descending: false));
            }
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            // Try composite cursor first
            Dictionary<string, string>? compositeValues = CursorEncoder.DecodeComposite(cursor, logger);

            if (compositeValues is not null && sortFields.Count > 0)
            {
                // Composite cursor: build compound WHERE from sort fields
                Expression<Func<T, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<T>(
                    sortFields, compositeValues, logger);

                if (predicate is not null)
                {
                    query = query.Where(predicate);
                }
            }
            else
            {
                // Legacy single-field cursor: WHERE cursorProperty > cursorValue
                string? cursorValue = CursorEncoder.Decode<string>(cursor, logger);
                if (cursorValue is not null)
                {
                    object? converted = FilterExpressionBuilder.ConvertValue(
                        cursorValue,
                        Nullable.GetUnderlyingType(cursorProperty.PropertyType) ?? cursorProperty.PropertyType,
                        logger, cursorPropertyName);

                    if (converted is not null)
                    {
                        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
                        MemberExpression member = Expression.Property(parameter, cursorProperty);
                        ConstantExpression constant = Expression.Constant(converted, cursorProperty.PropertyType);
                        BinaryExpression greaterThan = Expression.GreaterThan(member, constant);
                        var predicate = Expression.Lambda<Func<T, bool>>(greaterThan, parameter);
                        query = query.Where(predicate);
                    }
                }
            }
        }

        // Take pageSize + 1 to determine if there are more pages
        List<T> items = await query
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? nextCursor = null;
        bool hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
            T lastItem = items[^1];

            if (sortFields.Count > 0)
            {
                // Composite cursor: encode all sort field values
                nextCursor = CompositeCursorBuilder.EncodeCompositeCursor(lastItem, sortFields);
            }
            else
            {
                // Legacy single-field cursor
                object? lastValue = cursorProperty.GetValue(lastItem);
                if (lastValue is not null)
                {
                    nextCursor = CursorEncoder.Encode(lastValue.ToString()!);
                }
            }
        }

        return new PagedResult<T>(items, TotalCount: null, HasMore: hasMore, NextCursor: nextCursor);
    }
}
