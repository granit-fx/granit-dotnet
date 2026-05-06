using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Integration event mirroring <see cref="ActivityReassignedEvent"/> for
/// distributed dispatch via Wolverine outbox.
/// </summary>
public sealed record ActivityReassignedEto(
    Guid ActivityId,
    Guid PreviousAssigneeUserId,
    Guid NewAssigneeUserId,
    Guid? TenantId) : IIntegrationEvent;
