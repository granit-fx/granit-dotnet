using Granit.Events;

namespace Granit.OpenIddict.Events;

/// <summary>
/// Integration event published when a user account is deleted (GDPR right to erasure).
/// </summary>
/// <param name="UserId">The deleted user's unique identifier.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
public sealed record AccountDeletedEto(
    Guid UserId,
    Guid? TenantId) : IIntegrationEvent;
