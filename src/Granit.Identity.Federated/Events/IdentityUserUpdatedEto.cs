using Granit.Events;

namespace Granit.Identity.Federated.Events;

/// <summary>
/// Wolverine message published when an identity provider user is created or updated.
/// Provider-agnostic: the application host translates provider-specific webhooks
/// (Keycloak admin events, Entra ID change notifications, etc.) into this event.
/// </summary>
/// <param name="UserId">The external user ID in the identity provider.</param>
public sealed record IdentityUserUpdatedEto(string UserId) : IIntegrationEvent;
