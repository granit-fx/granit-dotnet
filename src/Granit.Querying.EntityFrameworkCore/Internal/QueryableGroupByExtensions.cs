using System.Linq.Expressions;
using System.Reflection;
using Granit.Querying.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Querying.EntityFrameworkCore.Internal;

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
        CancellationToken cancellationToken)
        where T : class
    {
        PropertyInfo? property = typeof(T).GetProperty(
            groupByField,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null)
        {
            return new GroupedResult<T>([], 0);
        }

        // Verify it's a whitelisted group-by field
        bool isAllowed = builder.GroupByFields
            .Any(g => g.PropertyName.Equals(groupByField, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            return new GroupedResult<T>([], 0);
        }

        // Build dynamic GroupBy: source.GroupBy(e => e.Property)
        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        MemberExpression member = Expression.Property(parameter, property);
        LambdaExpression keySelector = Expression.Lambda(member, parameter);

        // Use runtime GroupBy via reflection to support dynamic key types
        MethodInfo groupByMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == nameof(Queryable.GroupBy)
                && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.PropertyType);

        object groupedQuery = groupByMethod.Invoke(null, [source, keySelector])!;

        // Materialize groups with count
        // We need to project to a known type
        List<GroupEntry<T>> groups = await MaterializeGroupsAsync<T>(
            groupedQuery, property, groupByField, cancellationToken).ConfigureAwait(false);

        int totalCount = groups.Sum(g => g.Count);

        return new GroupedResult<T>(groups, totalCount);
    }

    private static async Task<List<GroupEntry<T>>> MaterializeGroupsAsync<T>(
        object groupedQuery,
        PropertyInfo property,
        string fieldName,
        CancellationToken cancellationToken)
        where T : class
    {
        // Use dynamic approach to handle different key types
        Type keyType = property.PropertyType;
        Type groupingType = typeof(IGrouping<,>).MakeGenericType(keyType, typeof(T));
        // Select each group's key and count
        // Build: groups.Select(g => new { Key = g.Key, Count = g.Count() })
        ParameterExpression gParam = Expression.Parameter(groupingType, "g");

        MemberExpression keyAccess = Expression.Property(gParam, "Key");
        MethodInfo countMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(T));

        Expression countCall = Expression.Call(countMethod, gParam);

        // Create anonymous type proxy
        Type resultType = typeof(GroupKeyCount<>).MakeGenericType(keyType);
        ConstructorInfo ctor = resultType.GetConstructors().First();
        NewExpression newExpr = Expression.New(ctor, keyAccess, countCall);
        LambdaExpression selectLambda = Expression.Lambda(newExpr, gParam);

        MethodInfo selectMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
            .MakeGenericMethod(groupingType, resultType);

        object projected = selectMethod.Invoke(null, [groupedQuery, selectLambda])!;

        // Materialize via ToListAsync
        MethodInfo toListAsync = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                && m.GetParameters().Length == 2)
            .MakeGenericMethod(resultType);

        dynamic task = toListAsync.Invoke(null, [projected, cancellationToken])!;
        var materialized = (System.Collections.IList)await task.ConfigureAwait(false);

        List<GroupEntry<T>> entries = [];
        PropertyInfo keyProp = resultType.GetProperty("Key")!;
        PropertyInfo countProp = resultType.GetProperty("Count")!;

        foreach (object item in materialized)
        {
            object? key = keyProp.GetValue(item);
            int count = (int)countProp.GetValue(item)!;
            string label = key?.ToString() ?? "(null)";

            entries.Add(new GroupEntry<T>
            {
                Field = fieldName,
                Value = key,
                Label = label,
                Count = count,
            });
        }

        return entries;
    }
}

/// <summary>
/// Helper type for materializing group key + count pairs.
/// </summary>
internal sealed class GroupKeyCount<TKey>(TKey key, int count)
{
    public TKey Key { get; } = key;
    public int Count { get; } = count;
}
