using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a user is added to or removed from a group.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="GroupId">The group ID.</param>
/// <param name="Added"><c>true</c> if the user was added; <c>false</c> if removed.</param>
public sealed record IdentityGroupMembershipChangedEto(string UserId, string GroupId, bool Added) : IIntegrationEvent;
