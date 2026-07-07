using System.Linq.Expressions;
using System.Reflection;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Extension methods for applying single-level GroupBy to an <see cref="IQueryable{T}"/>.
/// </summary>
internal static class QueryableGroupByExtensions
{
    /// <summary>
    /// Applies group-by and returns a <see cref="GroupedResult{T}"/>.
    /// </summary>
    public static async Task<GroupedResult<T>> ApplyGroupByAsync<T>(
        this IQueryable<T> source,
        string groupByField,
        QueryDefinitionBuilder<T> builder,
        int maxGroupCount,
        CancellationToken cancellationToken)
        where T : class
    {
        // Verify it's a whitelisted group-by field (dotted complex-type paths compared verbatim).
        bool isAllowed = builder.GroupByFields
            .Any(g => g.PropertyName.Equals(groupByField, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            return new GroupedResult<T>([], 0);
        }

        // Build dynamic GroupBy: source.GroupBy(e => e.Property) — or e.Value.Country for a dotted
        // complex-type path. A [QueryableValueObject] leaf groups by its real `.Value` scalar column
        // (ADR-070), so the key is the underlying primitive and GROUP BY translates.
        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        Expression? member = GroupByPathResolver.BuildKeyAccess(parameter, groupByField);

        if (member is null)
        {
            return new GroupedResult<T>([], 0);
        }

        LambdaExpression keySelector = Expression.Lambda(member, parameter);
        Type keyType = member.Type;

        // Use runtime GroupBy via reflection to support dynamic key types
        MethodInfo groupByMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == nameof(Queryable.GroupBy)
                && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), keyType);

        object groupedQuery = groupByMethod.Invoke(null, [source, keySelector])!;

        bool isAsyncProvider = source.Provider is Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider;

        // Materialize groups with count + declared aggregates in a single projection
        List<GroupEntry<T>> groups = await MaterializeGroupsAsync<T>(
            groupedQuery, keyType, groupByField, builder.Aggregates, maxGroupCount, isAsyncProvider, cancellationToken).ConfigureAwait(false);

        int totalCount = groups.Sum(g => g.Count);

        return new GroupedResult<T>(groups, totalCount);
    }

    private static async Task<List<GroupEntry<T>>> MaterializeGroupsAsync<T>(
        object groupedQuery,
        Type keyType,
        string fieldName,
        List<AggregateDescriptor> aggregates,
        int maxGroupCount,
        bool isAsyncProvider,
        CancellationToken cancellationToken)
        where T : class
    {
        // Use dynamic approach to handle different key types
        Type groupingType = typeof(IGrouping<,>).MakeGenericType(keyType, typeof(T));
        // Select each group's key, count, and declared aggregates:
        // groups.Select(g => new GroupKeyCount<TKey>(g.Key, g.Count(), g.Sum(x => (decimal?)x.Amount), …))
        ParameterExpression gParam = Expression.Parameter(groupingType, "g");

        MemberExpression keyAccess = Expression.Property(gParam, "Key");
        MethodInfo countMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(T));

        Expression countCall = Expression.Call(countMethod, gParam);

        // Fixed-slot projection type: every declared aggregate (max QueryEngineDefaults.MaxAggregates)
        // is computed as decimal? in the same SQL projection; unused slots stay null constants.
        Expression[] aggregateSlots = BuildAggregateSlots<T>(gParam, countCall, aggregates);

        Type resultType = typeof(GroupKeyCount<>).MakeGenericType(keyType);
        ConstructorInfo ctor = resultType.GetConstructors().First();
        NewExpression newExpr = Expression.New(ctor, [keyAccess, countCall, .. aggregateSlots]);
        LambdaExpression selectLambda = Expression.Lambda(newExpr, gParam);

        MethodInfo selectMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
            .MakeGenericMethod(groupingType, resultType);

        object projected = selectMethod.Invoke(null, [groupedQuery, selectLambda])!;

        // Apply cardinality limit to prevent memory exhaustion (CWE-770)
        MethodInfo takeMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2)
            .MakeGenericMethod(resultType);

        projected = takeMethod.Invoke(null, [projected, maxGroupCount])!;

        // Materialize — use async EF Core path for real DbContext sources, sync fallback for in-memory.
        System.Collections.IList materialized;
        if (isAsyncProvider)
        {
            MethodInfo toListAsync = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                    && m.GetParameters().Length == 2)
                .MakeGenericMethod(resultType);

            dynamic task = toListAsync.Invoke(null, [projected, cancellationToken])!;
            materialized = (System.Collections.IList)await task.ConfigureAwait(false);
        }
        else
        {
            MethodInfo toList = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == nameof(Enumerable.ToList) && m.GetParameters().Length == 1)
                .MakeGenericMethod(resultType);

            materialized = (System.Collections.IList)toList.Invoke(null, [projected])!;
        }

        List<GroupEntry<T>> entries = [];
        PropertyInfo keyProp = resultType.GetProperty("Key")!;
        PropertyInfo countProp = resultType.GetProperty("Count")!;
        PropertyInfo[] slotProps = [.. Enumerable.Range(0, aggregates.Count)
            .Select(i => resultType.GetProperty($"A{i}")!)];

        foreach (object item in materialized)
        {
            object? key = keyProp.GetValue(item);
            int count = (int)countProp.GetValue(item)!;
            string label = key?.ToString() ?? "(null)";

            Dictionary<string, object?>? aggregateValues = null;
            if (aggregates.Count > 0)
            {
                aggregateValues = new(aggregates.Count, StringComparer.Ordinal);
                for (int i = 0; i < aggregates.Count; i++)
                {
                    aggregateValues[aggregates[i].Alias] = slotProps[i].GetValue(item);
                }
            }

            entries.Add(new GroupEntry<T>
            {
                Field = fieldName,
                Value = key,
                Label = label,
                Count = count,
                Aggregates = aggregateValues,
            });
        }

        return entries;
    }

    /// <summary>
    /// Builds one <c>decimal?</c> expression per declared aggregate — <c>g.Sum(x => (decimal?)x.Prop)</c>,
    /// <c>g.Average(…)</c>, <c>g.Min(…)</c>, <c>g.Max(…)</c>, or the group count for
    /// <see cref="AggregateFunction.Count"/> — padding the remaining fixed slots with null constants.
    /// The builder guarantees numeric properties and the slot cap
    /// (<see cref="QueryEngineDefaults.MaxAggregates"/>), so the decimal? conversion always translates.
    /// </summary>
    private static Expression[] BuildAggregateSlots<T>(
        ParameterExpression gParam,
        Expression countCall,
        List<AggregateDescriptor> aggregates)
        where T : class
    {
        var slots = new Expression[QueryEngineDefaults.MaxAggregates];
        ConstantExpression nullSlot = Expression.Constant(null, typeof(decimal?));

        for (int i = 0; i < slots.Length; i++)
        {
            if (i >= aggregates.Count)
            {
                slots[i] = nullSlot;
                continue;
            }

            AggregateDescriptor descriptor = aggregates[i];

            if (descriptor.Function is AggregateFunction.Count)
            {
                slots[i] = Expression.Convert(countCall, typeof(decimal?));
                continue;
            }

            PropertyInfo? property = typeof(T).GetProperty(
                descriptor.PropertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null)
            {
                // Property vanished between declaration and execution (renamed entity member):
                // surface a null aggregate rather than throwing inside the SQL projection.
                slots[i] = nullSlot;
                continue;
            }

            ParameterExpression xParam = Expression.Parameter(typeof(T), "x");
            Expression selectorBody = Expression.Convert(
                Expression.Property(xParam, property), typeof(decimal?));
            LambdaExpression selector = Expression.Lambda(selectorBody, xParam);

            string methodName = descriptor.Function switch
            {
                AggregateFunction.Sum => nameof(Enumerable.Sum),
                AggregateFunction.Avg => nameof(Enumerable.Average),
                AggregateFunction.Min => nameof(Enumerable.Min),
                AggregateFunction.Max => nameof(Enumerable.Max),
                _ => nameof(Enumerable.Sum),
            };

            // The Func<TSource, decimal?> overload of each aggregate (returns decimal?).
            MethodInfo aggregateMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == methodName
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType.IsGenericType
                    && m.GetParameters()[1].ParameterType.GetGenericArguments() is [_, Type ret]
                    && ret == typeof(decimal?))
                .MakeGenericMethod(typeof(T));

            slots[i] = Expression.Call(aggregateMethod, gParam, selector);
        }

        return slots;
    }
}

/// <summary>
/// Helper type for materializing group key + count + fixed aggregate slots.
/// Slot count mirrors <see cref="QueryEngineDefaults.MaxAggregates"/>.
/// </summary>
internal sealed class GroupKeyCount<TKey>(
    TKey key,
    int count,
    decimal? a0,
    decimal? a1,
    decimal? a2,
    decimal? a3,
    decimal? a4,
    decimal? a5,
    decimal? a6,
    decimal? a7)
{
    public TKey Key { get; } = key;
    public int Count { get; } = count;
    public decimal? A0 { get; } = a0;
    public decimal? A1 { get; } = a1;
    public decimal? A2 { get; } = a2;
    public decimal? A3 { get; } = a3;
    public decimal? A4 { get; } = a4;
    public decimal? A5 { get; } = a5;
    public decimal? A6 { get; } = a6;
    public decimal? A7 { get; } = a7;
}
