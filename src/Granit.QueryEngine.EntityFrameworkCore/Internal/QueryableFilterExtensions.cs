using System.Linq.Expressions;
using Granit.QueryEngine.Filtering;
using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Extension methods for applying filters, global search, and presets to an <see cref="IQueryable{T}"/>.
/// </summary>
internal static class QueryableFilterExtensions
{
    /// <summary>
    /// Applies parsed filter criteria to the queryable, validating each field against the
    /// whitelist of filterable columns declared in the definition builder.
    /// </summary>
    public static IQueryable<TEntity> ApplyFilters<TEntity>(
        this IQueryable<TEntity> source,
        IReadOnlyList<FilterCriteria> criteria,
        QueryDefinitionBuilder<TEntity> builder,
        ILogger? logger = null)
        where TEntity : class
    {
        var filterableFields = builder.Columns
            .Where(c => c.IsFilterable)
            .Select(c => c.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build a lookup for shadow columns (name → descriptor) for FilterExpressionBuilder
        Dictionary<string, ColumnDescriptor>? shadowColumns = null;
        var shadows = builder.Columns.Where(c => c.IsShadowProperty && c.IsFilterable).ToList();
        if (shadows.Count > 0)
        {
            shadowColumns = shadows.ToDictionary(c => c.PropertyName, StringComparer.OrdinalIgnoreCase);
        }

        IQueryable<TEntity> query = source;

        foreach (FilterCriteria criterion in criteria)
        {
            if (!filterableFields.Contains(criterion.Field))
            {
                continue; // Silently ignore non-whitelisted fields
            }

            Expression<Func<TEntity, bool>>? predicate =
                FilterExpressionBuilder.Build<TEntity>(criterion, logger, shadowColumns);

            if (predicate is not null)
            {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    /// <summary>
    /// Applies global free-text search across all declared global search properties.
    /// Uses OR semantics (any property matching satisfies the search).
    /// Delegates to <see cref="ContainsSearchStrategy{TEntity}"/> for the actual implementation.
    /// </summary>
    public static IQueryable<TEntity> ApplyGlobalSearch<TEntity>(
        this IQueryable<TEntity> source,
        string searchTerm,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class =>
        new ContainsSearchStrategy<TEntity>().ApplySearch(source, searchTerm, builder.GlobalSearchProperties);

    /// <summary>
    /// Applies preset filters. OR within each group, AND between groups.
    /// </summary>
    public static IQueryable<TEntity> ApplyPresets<TEntity>(
        this IQueryable<TEntity> source,
        IReadOnlyDictionary<string, string>? activePresets,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class
    {
        if (activePresets is null || activePresets.Count == 0)
        {
            // Apply default presets
            return ApplyDefaultPresets(source, builder);
        }

        IQueryable<TEntity> query = source;

        foreach (FilterGroupDescriptor group in builder.FilterGroups)
        {
            if (!activePresets.TryGetValue(group.Name, out string? presetNames))
            {
                continue;
            }

            HashSet<string> active = presetNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Expression<Func<TEntity, bool>>? groupPredicate = BuildGroupPredicate<TEntity>(group, active);
            if (groupPredicate is not null)
            {
                query = query.Where(groupPredicate);
            }
        }

        return query;
    }

    private static IQueryable<TEntity> ApplyDefaultPresets<TEntity>(
        IQueryable<TEntity> source,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class
    {
        IQueryable<TEntity> query = source;

        foreach (FilterGroupDescriptor group in builder.FilterGroups)
        {
            var defaults = group.Presets
                .Where(p => p.IsDefault)
                .ToList();

            if (defaults.Count == 0)
            {
                continue;
            }

            var defaultNames = defaults.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Expression<Func<TEntity, bool>>? predicate = BuildGroupPredicate<TEntity>(group, defaultNames);
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    private static Expression<Func<TEntity, bool>>? BuildGroupPredicate<TEntity>(
        FilterGroupDescriptor group,
        HashSet<string> activeNames)
        where TEntity : class
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression? combined = null;

        foreach (PresetDescriptor preset in group.Presets)
        {
            if (!activeNames.Contains(preset.Name))
            {
                continue;
            }

            // Rebind the preset predicate to use our parameter
            var typedPredicate = (Expression<Func<TEntity, bool>>)preset.Predicate;
            Expression body = new ParameterReplacer(typedPredicate.Parameters[0], parameter)
                .Visit(typedPredicate.Body);

            combined = combined is null ? body : Expression.OrElse(combined, body);
        }

        if (combined is null)
        {
            return null;
        }

        return Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
    }

    /// <summary>
    /// Applies quick filters to the queryable. Quick filters are independent toggleable predicates
    /// that combine with AND semantics. When no quick filters are explicitly requested, default
    /// quick filters (those with <c>IsDefault = true</c>) are applied.
    /// </summary>
    public static IQueryable<TEntity> ApplyQuickFilters<TEntity>(
        this IQueryable<TEntity> source,
        IReadOnlyList<string>? activeQuickFilters,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class
    {
        if (builder.QuickFilters.Count == 0)
        {
            return source;
        }

        IQueryable<TEntity> query = source;

        if (activeQuickFilters is null || activeQuickFilters.Count == 0)
        {
            // Apply default quick filters
            foreach (QuickFilterDescriptor filter in builder.QuickFilters)
            {
                if (filter.IsDefault)
                {
                    query = query.Where((Expression<Func<TEntity, bool>>)filter.Predicate);
                }
            }

            return query;
        }

        // Apply explicitly requested quick filters (AND semantics)
        var active = activeQuickFilters
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (QuickFilterDescriptor filter in builder.QuickFilters.Where(f => active.Contains(f.Name)))
        {
            query = query.Where((Expression<Func<TEntity, bool>>)filter.Predicate);
        }

        return query;
    }

    /// <summary>
    /// Replaces one parameter expression with another in an expression tree.
    /// </summary>
    private sealed class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == oldParam ? newParam : base.VisitParameter(node);
    }
}
