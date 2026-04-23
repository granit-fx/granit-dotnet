using Granit.Events;

namespace Granit.Identity.Federated.Events;

/// <summary>
/// Raised when a user cache entry is hard-deleted (GDPR Art. 17 erasure).
/// Enables audit trail of personal data deletion for ISO 27001 compliance.
/// </summary>
/// <param name="ExternalUserId">The identity provider user identifier.</param>
/// <param name="TenantId">Tenant scope. <c>null</c> for host-level entries.</param>
public sealed record UserCacheEntryErasedEvent(
    string ExternalUserId,
    Guid? TenantId) : IDomainEvent;
