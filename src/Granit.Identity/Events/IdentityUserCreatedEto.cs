using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a new user is created in the identity provider.
/// </summary>
/// <param name="UserId">The provider-assigned user ID.</param>
/// <param name="Username">The username of the created user (may be null).</param>
/// <param name="Email">The email of the created user (may be null).</param>
public sealed record IdentityUserCreatedEto(string UserId, string? Username, string? Email) : IIntegrationEvent;
