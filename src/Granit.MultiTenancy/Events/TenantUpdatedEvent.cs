using Granit.Events;

namespace Granit.MultiTenancy.Events;

/// <summary>
/// Raised when a tenant's details are updated.
/// </summary>
/// <param name="TenantId">The unique identifier of the tenant.</param>
/// <param name="Name">Updated display name.</param>
/// <param name="ContactEmail">Updated contact email (or <c>null</c>).</param>
public sealed record TenantUpdatedEvent(
    Guid TenantId,
    string Name,
    string? ContactEmail) : IDomainEvent;
