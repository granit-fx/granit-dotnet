using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a user's sessions are revoked.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
public sealed record IdentitySessionsRevokedEto(string UserId) : IIntegrationEvent;
