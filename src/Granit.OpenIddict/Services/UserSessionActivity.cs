namespace Granit.OpenIddict.Services;

/// <summary>
/// Represents the last activity timestamp for a user session, used for idle session timeout.
/// </summary>
/// <remarks>
/// Stored in <c>IFusionCache</c> with key <c>session:{userId}:{jti}</c>.
/// Cache TTL = <c>IdleSessionTimeout + 5 min</c>.
/// </remarks>
/// <param name="UserId">The user identifier.</param>
/// <param name="Jti">The JWT token identifier (unique per refresh token).</param>
/// <param name="LastActivityAt">The UTC timestamp of the last heartbeat.</param>
public sealed record UserSessionActivity(
    string UserId,
    string Jti,
    DateTimeOffset LastActivityAt);
