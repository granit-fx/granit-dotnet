using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>
/// Raised by both the moved aggregate and the service after a bulk descendant
/// re-materialisation. The service-emitted variant carries the count of affected
/// descendant rows so downstream consumers can issue prefix-based cache invalidations
/// rather than N point invalidations.
/// </summary>
public sealed record CategoryTreePathChangedEvent(
    Guid CategoryId,
    Guid? TenantId,
    string OldPath,
    string NewPath,
    int AffectedDescendantCount = 0) : IDomainEvent;
