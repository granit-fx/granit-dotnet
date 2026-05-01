using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Layouts;

/// <summary>
/// Inclusive time window for a calendar range query — duplicated here so the
/// filter builder stays independent of <c>Granit.Entities.Endpoints</c>.
/// </summary>
public readonly record struct CalendarFilterRange(DateTimeOffset From, DateTimeOffset To);

/// <summary>
/// Composes the LINQ filter expression that a calendar runner applies to its
/// underlying <see cref="IQueryable{T}"/>. The expression keys on the layout's
/// <see cref="CalendarLayoutDescriptor.StartPropertyName"/> (and optional
/// <see cref="CalendarLayoutDescriptor.EndPropertyName"/>) — never on hard-coded
/// property names — so the same builder serves any entity that exposes a
/// calendar layout.
/// </summary>
/// <remarks>
/// <para>
/// The builder produces an <b>additive</b> predicate, intended to be composed
/// with <c>Where(...)</c> on top of the base queryable, AFTER any entity-baked
/// filters declared on the underlying <c>QueryDefinition</c>. The range filter
/// can only narrow the result set — it never widens it. This matches the
/// additive-only contract from ADR-048 §4 (cross-workspace presets).
/// </para>
/// <para>
/// Overlap semantics — adopted from the standard interval-overlap formula:
/// </para>
/// <list type="bullet">
///   <item><description>
///     Events with a non-null end (<c>EndField</c>) overlap the window
///     <c>[from, to]</c> when <c>Start &lt;= to</c> AND <c>from &lt;= End</c>.
///   </description></item>
///   <item><description>
///     Point-in-time events (no <c>EndField</c>, or End is <see langword="null"/>)
///     overlap when <c>from &lt;= Start &lt;= to</c>.
///   </description></item>
/// </list>
/// <para>
/// The two cases are unified into a single SQL-translatable expression by
/// coalescing <c>End</c> to <c>Start</c> on the right-hand bound:
/// <c>Start &lt;= to AND from &lt;= (End ?? Start)</c>.
/// </para>
/// </remarks>
public static class CalendarRangeFilterBuilder
{
    /// <summary>
    /// Builds an <c>Expression&lt;Func&lt;TEntity, bool&gt;&gt;</c> that retains
    /// only the events overlapping <paramref name="range"/>, keyed on the
    /// property names declared by <paramref name="layout"/>.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the layout was declared on.</typeparam>
    /// <param name="layout">Calendar layout descriptor naming the start / end properties.</param>
    /// <param name="range">Inclusive time window.</param>
    /// <returns>A LINQ expression suitable for <c>IQueryable&lt;TEntity&gt;.Where(...)</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the layout names a property that is not declared on
    /// <typeparamref name="TEntity"/> — surfaces a typo from the
    /// <c>CalendarView(...)</c> declaration (defensive: the builder guards
    /// against ToString-based bypasses but cannot catch property renames at
    /// compile time once the descriptor is built).
    /// </exception>
    public static Expression<Func<TEntity, bool>> BuildOverlapFilter<TEntity>(
        CalendarLayoutDescriptor layout,
        CalendarFilterRange range)
    {
        ArgumentNullException.ThrowIfNull(layout);

        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression startMember = ResolvePropertyAccess(entity, layout.StartPropertyName, "StartField");
        ConstantExpression from = Expression.Constant(range.From, typeof(DateTimeOffset));
        ConstantExpression to = Expression.Constant(range.To, typeof(DateTimeOffset));

        Expression body;
        if (layout.EndPropertyName is null)
        {
            // Point-in-time events only: from <= Start <= to.
            body = Expression.AndAlso(
                Expression.GreaterThanOrEqual(startMember, from),
                Expression.LessThanOrEqual(startMember, to));
        }
        else
        {
            MemberExpression endMember = ResolvePropertyAccess(entity, layout.EndPropertyName, "EndField");
            // Coalesce End to Start so the right-hand bound is always a non-null
            // DateTimeOffset; SQL providers translate (End ?? Start) to COALESCE
            // without needing an explicit branch.
            Expression rightBound = endMember.Type == typeof(DateTimeOffset?)
                ? Expression.Coalesce(endMember, startMember)
                : endMember;

            body = Expression.AndAlso(
                Expression.LessThanOrEqual(startMember, to),
                Expression.LessThanOrEqual(from, rightBound));
        }

        return Expression.Lambda<Func<TEntity, bool>>(body, entity);
    }

    private static MemberExpression ResolvePropertyAccess(
        ParameterExpression entity,
        string propertyName,
        string dslSlot)
    {
        PropertyInfo? property = entity.Type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Calendar {dslSlot}='{propertyName}' is not a public instance property on '{entity.Type.FullName}'. "
                + "The descriptor was likely built for a different type, or the property was renamed since the EntityDefinition was declared.");
        }

        return Expression.Property(entity, property);
    }
}
