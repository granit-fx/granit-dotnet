using System.Linq.Expressions;
using Granit.QueryEngine;

namespace Granit.Analytics.Metrics;

/// <summary>
/// Specialised <see cref="MetricDefinition{TEntity, TValue}"/> that aggregates
/// over a projection involving a JOIN to a second entity type
/// <typeparamref name="TJoined"/>. Use this when the value being aggregated is
/// not a column on <typeparamref name="TEntity"/> but is computed from a
/// related row — e.g. SaaS Monthly Recurring Revenue, where each
/// <c>Subscription</c>'s monthly contribution is derived from its bound
/// <c>PlanPrice.Amount</c> + <c>PlanPrice.Interval</c>.
/// </summary>
/// <typeparam name="TEntity">The base entity the metric scopes against (filtered by <c>BaseFilter</c>, <c>QueryEngine</c> pipeline, multi-tenant + soft-delete).</typeparam>
/// <typeparam name="TJoined">The joined entity providing the projected value(s).</typeparam>
/// <typeparam name="TValue">Aggregation result type.</typeparam>
/// <remarks>
/// <para>
/// The framework's <c>MetricExecutor</c> detects subclasses through the
/// <see cref="IJoinedMetricProjector{TEntity, TValue}"/> marker interface,
/// resolves an <see cref="IQueryableSource{T}"/> for <typeparamref name="TJoined"/>
/// at request time, and calls <see cref="Project"/> to obtain the projected
/// <see cref="IQueryable{T}"/>. The aggregation step (<c>Sum</c> /
/// <c>Avg</c> / <c>Min</c> / <c>Max</c>) then runs on the projection — same
/// per-type EF Core dispatch and same empty-set semantics as the single-table
/// path (story A3 #1374).
/// </para>
/// <para>
/// <see cref="MetricDefinition{TEntity, TValue}.Selector"/> is unused for
/// joined metrics — <see cref="Project"/> takes its place. <c>Aggregation</c>
/// = <see cref="Granit.QueryEngine.Filtering.AggregateFunction.Count"/> is
/// also unsupported (count doesn't need a join — use a regular
/// <see cref="MetricDefinition{TEntity, TValue}"/> instead).
/// </para>
/// </remarks>
public abstract class JoinedMetricDefinition<TEntity, TJoined, TValue>
    : MetricDefinition<TEntity, TValue>, IJoinedMetricProjector<TEntity, TValue>
    where TEntity : class
    where TJoined : class
    where TValue : struct
{
    /// <summary>
    /// Build the projected <see cref="IQueryable{T}"/> from the filtered
    /// <typeparamref name="TEntity"/> source (after <c>BaseFilter</c> + the
    /// QueryEngine pipeline) and the <typeparamref name="TJoined"/> source
    /// (DI-resolved <see cref="IQueryableSource{T}"/>). The author writes the
    /// LINQ join + projection here; the framework runs the aggregation on the
    /// resulting <see cref="IQueryable{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// public override IQueryable&lt;decimal?&gt; Project(IQueryable&lt;Subscription&gt; source, IQueryable&lt;PlanPrice&gt; joined) =&gt;
    ///     from s in source
    ///     join p in joined on s.PlanPriceId equals p.Id
    ///     select (decimal?)(p.Interval == BillingInterval.Yearly ? p.Amount / 12m
    ///                     : p.Interval == BillingInterval.Quarterly ? p.Amount / 3m
    ///                     : p.Amount);
    /// </code>
    /// </example>
    public abstract IQueryable<TValue?> Project(
        IQueryable<TEntity> filteredSource,
        IQueryable<TJoined> joinedSource);

    /// <summary>
    /// Joined metrics drive aggregation through <see cref="Project"/>; the
    /// <see cref="MetricDefinition{TEntity, TValue}.Selector"/> hook is unused
    /// and sealed to <see langword="null"/> to prevent accidental override.
    /// </summary>
    public sealed override Expression<Func<TEntity, TValue?>>? Selector => null;

    IQueryable<TValue?> IJoinedMetricProjector<TEntity, TValue>.ProjectFromServiceProvider(
        IQueryable<TEntity> filteredSource,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(filteredSource);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var joinedSource =
            (IQueryableSource<TJoined>?)serviceProvider.GetService(typeof(IQueryableSource<TJoined>));
        if (joinedSource is null)
        {
            throw new InvalidOperationException(
                $"Joined metric '{Name}' requires an IQueryableSource<{typeof(TJoined).Name}> " +
                "to be registered in DI — typically via the joined module's "
                + "AddGranit{Module}EntityFrameworkCore() extension. Without it the executor "
                + "cannot resolve the joined queryable.");
        }

        return Project(filteredSource, joinedSource.GetQueryable());
    }
}

/// <summary>
/// Type-erased projector surface for <see cref="JoinedMetricDefinition{TEntity, TJoined, TValue}"/>
/// — lets the <c>MetricExecutor</c> drive the projection without statically
/// knowing the joined entity type. Internal implementation detail; concrete
/// metrics derive from <see cref="JoinedMetricDefinition{TEntity, TJoined, TValue}"/>
/// directly and never implement this interface manually.
/// </summary>
/// <typeparam name="TEntity">The base entity type.</typeparam>
/// <typeparam name="TValue">Aggregation result type.</typeparam>
public interface IJoinedMetricProjector<TEntity, TValue>
    where TEntity : class
    where TValue : struct
{
    /// <summary>
    /// Resolve the joined source from <paramref name="serviceProvider"/> and
    /// invoke <see cref="JoinedMetricDefinition{TEntity, TJoined, TValue}.Project"/>.
    /// </summary>
    IQueryable<TValue?> ProjectFromServiceProvider(
        IQueryable<TEntity> filteredSource,
        IServiceProvider serviceProvider);
}
