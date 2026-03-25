using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a user's password is reset (temporary password set or reset email sent).
/// </summary>
/// <param name="UserId">The user ID in the identity provider.</param>
public sealed record IdentityPasswordResetEto(string UserId) : IIntegrationEvent;
