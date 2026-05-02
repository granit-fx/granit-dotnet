using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Document</c> is moved to the trash (soft-delete via domain status).
/// </summary>
public sealed record DocumentTrashedEvent(
    Guid DocumentId,
    Guid? TenantId,
    DateTimeOffset TrashedAt) : IDomainEvent;
