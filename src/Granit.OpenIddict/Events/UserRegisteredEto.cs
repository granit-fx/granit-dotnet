using Granit.Events;

namespace Granit.OpenIddict.Events;

/// <summary>
/// Integration event published when a new user registers.
/// </summary>
/// <param name="UserId">The newly registered user's unique identifier.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
public sealed record UserRegisteredEto(
    Guid UserId,
    string Email,
    Guid? TenantId) : IIntegrationEvent;
