using Granit.Events;

namespace Granit.Identity.Local.Events;

/// <summary>
/// Integration event published when two-factor authentication is enabled or disabled
/// on a user account.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Enabled"><see langword="true"/> if 2FA was enabled; <see langword="false"/> if disabled.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
public sealed record TwoFactorChangedEto(
    Guid UserId,
    bool Enabled,
    Guid? TenantId) : IIntegrationEvent;
