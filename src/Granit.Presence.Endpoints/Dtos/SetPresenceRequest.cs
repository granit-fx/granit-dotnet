using Granit.Presence.Domain;

namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Request payload to set or clear the caller's manual presence override.
/// </summary>
/// <param name="ManualStatus">Desired status. <see cref="ManualPresenceStatus.Available"/> clears the override.</param>
/// <param name="UntilUtc">Optional override expiration (UTC). Past values are rejected.</param>
public sealed record SetPresenceRequest(
    ManualPresenceStatus ManualStatus,
    DateTimeOffset? UntilUtc = null);
