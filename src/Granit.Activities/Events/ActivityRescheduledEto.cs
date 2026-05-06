using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Integration event mirroring <see cref="ActivityRescheduledEvent"/> for
/// distributed dispatch via Wolverine outbox.
/// </summary>
public sealed record ActivityRescheduledEto(
    Guid ActivityId,
    DateTimeOffset PreviousDueAt,
    DateTimeOffset NewDueAt,
    Guid? TenantId) : IIntegrationEvent;
