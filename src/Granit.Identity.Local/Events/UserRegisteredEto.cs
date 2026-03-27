using Granit.Events;

namespace Granit.Identity.Local.Events;

/// <summary>
/// Integration event published when a new user registers.
/// </summary>
/// <remarks>
/// Does not include email or PII — subscribers can resolve user details
/// from the identity store by <paramref name="UserId"/> if needed (data minimization).
/// </remarks>
/// <param name="UserId">The newly registered user's unique identifier.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
public sealed record UserRegisteredEto(
    Guid UserId,
    Guid? TenantId) : IIntegrationEvent;
