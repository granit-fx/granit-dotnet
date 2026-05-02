using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised by the activities overdue-scan background job
/// (story A8) the first time it observes an activity past its due date.
/// Idempotency tracked on the row via <c>OverdueNotifiedAt</c> — the job
/// does not re-emit this event for the same activity within the polling
/// window.
/// </summary>
/// <param name="ActivityId">Activity id.</param>
/// <param name="Type">Activity type name.</param>
/// <param name="AssignedToUserId">Recipient.</param>
/// <param name="DueAt">Original due date — the email surfaces "overdue by N days".</param>
/// <param name="OverdueByDays">Whole days elapsed since <paramref name="DueAt"/> (<c>&gt;= 1</c>).</param>
/// <param name="EntityType">Polymorphic FK target — host entity wire identifier.</param>
/// <param name="EntityId">Polymorphic FK target — host entity row id.</param>
/// <param name="TenantId">Owning tenant id (null for tenantless activities).</param>
public sealed record ActivityOverdueEvent(
    Guid ActivityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    int OverdueByDays,
    string EntityType,
    Guid EntityId,
    Guid? TenantId) : IDomainEvent;
