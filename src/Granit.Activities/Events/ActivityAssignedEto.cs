using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Integration event mirroring <see cref="ActivityAssignedEvent"/> for
/// distributed dispatch via Wolverine outbox. Carries
/// <see cref="TenantId"/> so cross-service consumers can scope handling
/// without joining back to a tenant table.
/// </summary>
public sealed record ActivityAssignedEto(
    Guid ActivityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    string EntityType,
    Guid EntityId,
    Guid? TenantId) : IIntegrationEvent;
