using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a role is removed from a user.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="RoleName">The name of the removed role.</param>
public sealed record IdentityRoleRemovedEto(string UserId, string RoleName) : IIntegrationEvent;
