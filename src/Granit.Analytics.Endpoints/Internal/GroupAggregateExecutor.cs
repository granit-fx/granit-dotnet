using System.Linq.Expressions;
using System.Reflection;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Builds and executes a typed <c>GroupBy(...).Select(...)</c> expression
/// tree against an <see cref="IQueryable{T}"/>, dispatching at runtime on
/// the group-by key type and the aggregate field's underlying primitive
/// type. Pushes the entire aggregation to SQL — no in-memory streaming —
/// so dashboard charts scale linearly with the number of groups, not the
/// number of rows.
/// </summary>
/// <remarks>
/// <para>
/// The expression tree built per call is:
/// </para>
/// <code>
/// source
///     .GroupBy(x =&gt; x.&lt;GroupBy&gt;)                                  // typed via TKey reflection
///     .Select(g =&gt; new GroupBucket(
///         (object)g.Key,
///         (decimal?)((Sum|Avg|Min|Max)(x =&gt; (TValue?)x.&lt;Field&gt;) ?? 0)))  // Sum coalesces; Avg/Min/Max do not
///     .ToListAsync(ct);
/// </code>
/// <para>
/// EF Core translates this to <c>SELECT &lt;groupBy&gt;, &lt;agg&gt;(&lt;field&gt;)
/// FROM ... GROUP BY &lt;groupBy&gt;</c>. Empty-set semantics match
/// <c>MetricExecutor</c> (locked by tests #1374): Sum-of-empty is 0;
/// Avg/Min/Max over a group with all-null values surface as null on the
/// bucket value (frontend renders "—").
/// </para>
/// </remarks>
internal static class GroupAggregateExecutor
{
    /// <summary>Result row materialised by the EF projection.</summary>
    /// <param name="Key">Boxed group key — matches the runtime CLR type of the GroupBy property.</param>
    /// <param name="Value">Aggregate value coerced to <see cref="decimal"/>; <see langword="null"/> for empty Avg/Min/Max groups.</param>
    public sealed record GroupBucket(object? Key, decimal? Value);

    /// <summary>Supported aggregate field types.</summary>
    private static readonly HashSet<Type> SupportedNumericTypes =
        [typeof(int), typeof(long), typeof(decimal), typeof(double)];

    /// <summary>
    /// Verifies that <paramref name="valueUnderlying"/> is one of the
    /// supported primitives (int/long/decimal/double).
    /// </summary>
    public static bool IsSupportedValueType(Type valueUnderlying) =>
        SupportedNumericTypes.Contains(valueUnderlying);

    public static async Task<List<GroupBucket>> ExecuteAsync<TEntity>(
        IQueryable<TEntity> source,
        PropertyInfo groupProp,
        PropertyInfo valueProp,
        Type valueUnderlying,
        AggregateFunction aggregation,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        // Dispatch on TKey via reflection — TKey is the runtime CLR type of
        // the GroupBy property; closed once per call so the EF translator
        // sees a fully typed expression tree at execution time.
        Type keyType = groupProp.PropertyType;

        MethodInfo helper = typeof(GroupAggregateExecutor)
            .GetMethod(nameof(ExecuteTypedAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(TEntity), keyType);

        var task = (Task<List<GroupBucket>>)helper.Invoke(
            null,
            [source, groupProp, valueProp, valueUnderlying, aggregation, cancellationToken])!;

        return await task.ConfigureAwait(false);
    }

    private static async Task<List<GroupBucket>> ExecuteTypedAsync<TEntity, TKey>(
        IQueryable<TEntity> source,
        PropertyInfo groupProp,
        PropertyInfo valueProp,
        Type valueUnderlying,
        AggregateFunction aggregation,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        Expression<Func<TEntity, TKey>> keySelector = BuildKeySelector<TEntity, TKey>(groupProp);
        Expression<Func<IGrouping<TKey, TEntity>, GroupBucket>> projection =
            BuildProjection<TEntity, TKey>(valueProp, valueUnderlying, aggregation);

        return await source.GroupBy(keySelector).Select(projection).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Expression<Func<TEntity, TKey>> BuildKeySelector<TEntity, TKey>(PropertyInfo groupProp)
    {
        ParameterExpression x = Expression.Parameter(typeof(TEntity), "x");
        Expression access = Expression.Property(x, groupProp);
        return Expression.Lambda<Func<TEntity, TKey>>(access, x);
    }

    private static Expression<Func<IGrouping<TKey, TEntity>, GroupBucket>> BuildProjection<TEntity, TKey>(
        PropertyInfo valueProp,
        Type valueUnderlying,
        AggregateFunction aggregation)
    {
        ParameterExpression g = Expression.Parameter(typeof(IGrouping<TKey, TEntity>), "g");
        MemberExpression keyAccess = Expression.Property(g, "Key");
        Expression keyAsObject = Expression.Convert(keyAccess, typeof(object));

        LambdaExpression valueLambda = BuildValueLambda<TEntity>(valueProp, valueUnderlying);
        Expression aggregateValue = BuildAggregateValue<TEntity>(g, valueLambda, valueUnderlying, aggregation);

        ConstructorInfo ctor = typeof(GroupBucket).GetConstructor([typeof(object), typeof(decimal?)])!;
        NewExpression bucketNew = Expression.New(ctor, keyAsObject, aggregateValue);

        return Expression.Lambda<Func<IGrouping<TKey, TEntity>, GroupBucket>>(bucketNew, g);
    }

    private static LambdaExpression BuildValueLambda<TEntity>(PropertyInfo valueProp, Type valueUnderlying)
    {
        ParameterExpression x = Expression.Parameter(typeof(TEntity), "x");
        Expression access = Expression.Property(x, valueProp);

        Type nullableValueType = typeof(Nullable<>).MakeGenericType(valueUnderlying);
        if (valueProp.PropertyType != nullableValueType)
        {
            access = Expression.Convert(access, nullableValueType);
        }

        Type lambdaType = typeof(Func<,>).MakeGenericType(typeof(TEntity), nullableValueType);
        return Expression.Lambda(lambdaType, access, x);
    }

    private static Expression BuildAggregateValue<TEntity>(
        ParameterExpression gParam,
        LambdaExpression valueLambda,
        Type valueUnderlying,
        AggregateFunction aggregation)
    {
        MethodInfo method = ResolveEnumerableMethod(aggregation, valueUnderlying)
            .MakeGenericMethod(typeof(TEntity));

        MethodCallExpression call = Expression.Call(method, gParam, valueLambda);

        // Sum-of-empty = 0 (mathematical identity, locked by #1374). Avg / Min /
        // Max keep null on empty groups so the frontend renders "—".
        Expression result = aggregation == AggregateFunction.Sum
            ? CoalesceToZero(call)
            : call;

        // Coerce every primitive (int? / long? / decimal? / double?) to decimal?
        // for the wire envelope. EF Core translates the cast to a SQL CAST.
        if (result.Type != typeof(decimal?))
        {
            result = Expression.Convert(result, typeof(decimal?));
        }

        return result;
    }

    private static MethodInfo ResolveEnumerableMethod(AggregateFunction aggregation, Type valueUnderlying)
    {
        string name = aggregation switch
        {
            AggregateFunction.Sum => nameof(Enumerable.Sum),
            AggregateFunction.Avg => nameof(Enumerable.Average),
            AggregateFunction.Min => nameof(Enumerable.Min),
            AggregateFunction.Max => nameof(Enumerable.Max),
            _ => throw new NotSupportedException(
                $"Aggregation '{aggregation}' is not supported by GroupAggregateExecutor."),
        };

        Type nullableValueType = typeof(Nullable<>).MakeGenericType(valueUnderlying);

        // Match Enumerable.<Sum/Average/Min/Max><TSource>(IEnumerable<TSource>, Func<TSource, TValue?>).
        // The per-primitive nullable overload is the right one for SQL translation —
        // it carries the null semantics needed for empty-group Avg/Min/Max.
        return typeof(Enumerable).GetMethods()
            .First(m => m.Name == name
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 1
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>)
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[1] == nullableValueType);
    }

    private static BinaryExpression CoalesceToZero(MethodCallExpression call)
    {
        Type returnType = call.Type;                                    // TValue?
        Type underlying = Nullable.GetUnderlyingType(returnType)
            ?? throw new InvalidOperationException("Aggregate return type was not nullable — cannot coalesce.");
        object zero = Activator.CreateInstance(underlying)
            ?? throw new InvalidOperationException("Failed to instantiate zero value.");
        ConstantExpression nullableZero = Expression.Constant(zero, returnType);
        return Expression.Coalesce(call, nullableZero);
    }
}
