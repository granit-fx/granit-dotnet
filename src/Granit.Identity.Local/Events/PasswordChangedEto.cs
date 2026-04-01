using Granit.Events;

namespace Granit.Identity.Local.Events;

/// <summary>
/// Integration event published when a user's password is changed.
/// </summary>
/// <remarks>
/// Consumed by notification modules to send a security alert email,
/// enabling the user to detect unauthorized password changes (compromise detection).
/// </remarks>
/// <param name="UserId">The user identifier.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
public sealed record PasswordChangedEto(
    Guid UserId,
    Guid? TenantId) : IIntegrationEvent;
