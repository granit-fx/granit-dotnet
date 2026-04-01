using Granit.Events;

namespace Granit.Identity.Local.Events;

/// <summary>
/// Integration event published when a user account is locked after exceeding the
/// maximum number of failed login attempts.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="FailedAttempts">Number of consecutive failed login attempts that triggered the lockout.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
public sealed record AccountLockedEto(
    Guid UserId,
    int FailedAttempts,
    Guid? TenantId) : IIntegrationEvent;
