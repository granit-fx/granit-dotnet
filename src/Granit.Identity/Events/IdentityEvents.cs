using Granit.Core.Events;
using Granit.Identity.Models;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a new user is created in the identity provider.
/// </summary>
/// <param name="UserId">The provider-assigned user ID.</param>
/// <param name="Username">The username of the created user (may be null).</param>
/// <param name="Email">The email of the created user (may be null).</param>
public sealed record IdentityUserCreatedEto(string UserId, string? Username, string? Email) : IIntegrationEvent;

/// <summary>
/// Published after a user's profile is updated in the identity provider.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="Update">The fields that were updated.</param>
public sealed record IdentityUserProfileUpdatedEto(string UserId, IdentityUserUpdate Update) : IIntegrationEvent;

/// <summary>
/// Published after a user account is enabled or disabled.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="Enabled">The new enabled state.</param>
public sealed record IdentityUserEnabledChangedEto(string UserId, bool Enabled) : IIntegrationEvent;

/// <summary>
/// Published after a role is assigned to a user.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="RoleName">The name of the assigned role.</param>
public sealed record IdentityRoleAssignedEto(string UserId, string RoleName) : IIntegrationEvent;

/// <summary>
/// Published after a role is removed from a user.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="RoleName">The name of the removed role.</param>
public sealed record IdentityRoleRemovedEto(string UserId, string RoleName) : IIntegrationEvent;

/// <summary>
/// Published after a user is added to or removed from a group.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="GroupId">The group ID.</param>
/// <param name="Added"><c>true</c> if the user was added; <c>false</c> if removed.</param>
public sealed record IdentityGroupMembershipChangedEto(string UserId, string GroupId, bool Added) : IIntegrationEvent;

/// <summary>
/// Published after a user's password is reset (temporary password set or reset email sent).
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
public sealed record IdentityPasswordResetEto(string UserId) : IIntegrationEvent;

/// <summary>
/// Published after a user's sessions are revoked.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
public sealed record IdentitySessionsRevokedEto(string UserId) : IIntegrationEvent;
