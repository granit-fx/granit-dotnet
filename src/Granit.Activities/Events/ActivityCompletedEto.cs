using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Integration event mirroring <see cref="ActivityCompletedEvent"/> for
/// distributed dispatch via Wolverine outbox.
/// </summary>
public sealed record ActivityCompletedEto(
    Guid ActivityId,
    Guid CompletedByUserId,
    DateTimeOffset CompletedAt,
    Guid? TenantId) : IIntegrationEvent;
