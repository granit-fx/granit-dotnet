using Granit.Events;

namespace Granit.MultiTenancy.Events;

/// <summary>
/// Raised when a tenant's custom domain is set or cleared.
/// Handlers should invalidate cached URL data for this tenant.
/// </summary>
/// <param name="TenantId">The unique identifier of the tenant.</param>
/// <param name="OldDomain">Previous custom domain, or <c>null</c> if none was set.</param>
/// <param name="NewDomain">New custom domain, or <c>null</c> if cleared.</param>
public sealed record TenantCustomDomainChangedEvent(
    Guid TenantId,
    string? OldDomain,
    string? NewDomain) : IDomainEvent;
