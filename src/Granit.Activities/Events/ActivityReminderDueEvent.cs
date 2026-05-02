using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised by the activities reminder background job (story A8)
/// the day before <see cref="Domain.Activity.DueAt"/>. Consumed by the
/// notification handler to dispatch a reminder to the assignee.
/// </summary>
/// <param name="ActivityId">Activity id.</param>
/// <param name="Type">Activity type name (for the email subject + body).</param>
/// <param name="AssignedToUserId">Recipient.</param>
/// <param name="DueAt">When the activity is due — the email surfaces this.</param>
/// <param name="EntityType">Polymorphic FK target — host entity wire identifier.</param>
/// <param name="EntityId">Polymorphic FK target — host entity row id.</param>
/// <param name="TenantId">Owning tenant id (null for tenantless activities).</param>
public sealed record ActivityReminderDueEvent(
    Guid ActivityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    string EntityType,
    Guid EntityId,
    Guid? TenantId) : IDomainEvent;
