using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a role is assigned to a user.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="RoleName">The name of the assigned role.</param>
public sealed record IdentityRoleAssignedEto(string UserId, string RoleName) : IIntegrationEvent;
