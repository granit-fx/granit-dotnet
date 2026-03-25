using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a user account is enabled or disabled.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="Enabled">The new enabled state.</param>
public sealed record IdentityUserEnabledChangedEto(string UserId, bool Enabled) : IIntegrationEvent;
