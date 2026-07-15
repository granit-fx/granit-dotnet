using Granit.Events;
using Granit.Identity.Models;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a user's profile is updated in the identity provider.
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
/// <param name="Update">The fields that were updated.</param>
public sealed record IdentityUserProfileUpdatedEto(string UserId, IdentityUserUpdate Update) : IIntegrationEvent;
