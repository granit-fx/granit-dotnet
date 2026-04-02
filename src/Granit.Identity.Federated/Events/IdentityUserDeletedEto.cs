using Granit.Events;

namespace Granit.Identity.Federated.Events;

/// <summary>
/// Wolverine message published when an identity provider user is deleted.
/// Provider-agnostic: the application host translates provider-specific webhooks
/// (Keycloak admin events, Entra ID change notifications, etc.) into this event.
/// Triggers a hard delete of the corresponding cache entry (RGPD Art. 17).
/// </summary>
/// <param name="UserId">The external user ID in the identity provider.</param>
/// <param name="TenantId">Optional tenant scope for the deletion. Null deletes across all tenants.</param>
public sealed record IdentityUserDeletedEto(string UserId, Guid? TenantId = null) : IIntegrationEvent;
