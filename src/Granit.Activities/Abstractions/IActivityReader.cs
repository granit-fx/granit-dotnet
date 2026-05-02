using Granit.Activities.Domain;

namespace Granit.Activities.Abstractions;

/// <summary>
/// Read operations for activities. Hosts wire <c>EfCoreActivityReader</c> via
/// <c>AddGranitActivitiesEntityFrameworkCore()</c>; tests can substitute a fake.
/// </summary>
public interface IActivityReader
{
    /// <summary>
    /// Returns the activity with the given id, or <see langword="null"/> if no
    /// such activity exists in the current tenant scope.
    /// </summary>
    Task<Activity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns activities matching <paramref name="filter"/>, ordered by
    /// <see cref="Activity.DueAt"/> ascending. Tenant-scoped via the standard
    /// EF Core query filter.
    /// </summary>
    /// <param name="filter">Filter predicate (entity / assignee / status / due-date window).</param>
    /// <param name="skip">Number of rows to skip (paging).</param>
    /// <param name="take">Page size — capped by the endpoint layer at <c>EntitiesEndpointsOptions.MaxPageSize</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Activity>> ListAsync(
        ActivityListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the total count matching <paramref name="filter"/> for paginated responses.</summary>
    Task<int> CountAsync(ActivityListFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read filter for <see cref="IActivityReader.ListAsync"/>. All fields are
/// optional — null values fall through, allowing the caller to combine any
/// subset.
/// </summary>
/// <param name="EntityType">Polymorphic FK target — only activities for this host entity wire identifier.</param>
/// <param name="EntityId">Polymorphic FK target — only activities for this host row id.</param>
/// <param name="AssignedToUserId">Only activities assigned to this user.</param>
/// <param name="Status">Only activities in this lifecycle state. Use <see cref="ActivityStatusFilter.OpenOrOverdue"/> for the inbox shortcut (Status == Open).</param>
/// <param name="DueAtFrom">Only activities with <c>DueAt &gt;= DueAtFrom</c>.</param>
/// <param name="DueAtTo">Only activities with <c>DueAt &lt; DueAtTo</c> (exclusive upper bound — half-open window).</param>
public sealed record ActivityListFilter(
    string? EntityType = null,
    Guid? EntityId = null,
    Guid? AssignedToUserId = null,
    ActivityStatusFilter? Status = null,
    DateTimeOffset? DueAtFrom = null,
    DateTimeOffset? DueAtTo = null);

/// <summary>
/// Three-state filter mirroring <see cref="ActivityStatus"/> with one
/// computed shortcut: <see cref="OpenOrOverdue"/> = <c>Status == Open</c>
/// (Overdue is never persisted, see ADR-046).
/// </summary>
public enum ActivityStatusFilter
{
    /// <summary>All open activities (includes those whose <c>DueAt</c> is in the past — i.e. "overdue").</summary>
    OpenOrOverdue = 0,

    /// <summary>Only completed activities.</summary>
    Done = 1,

    /// <summary>Only cancelled activities.</summary>
    Cancelled = 2,
}
