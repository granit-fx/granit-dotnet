namespace Granit.Presence.Endpoints.Permissions;

/// <summary>
/// Rate limiting policy names for the presence endpoints.
/// Configure limits in <c>RateLimiting:Policies</c> using these names as keys.
/// </summary>
/// <example>
/// <code>
/// "RateLimiting": {
///   "Policies": {
///     "presence-poll":   { "PermitLimit": 4,  "Window": "00:01:00" },
///     "presence-mutate": { "PermitLimit": 60, "Window": "00:01:00" },
///     "presence-query":  { "PermitLimit": 30, "Window": "00:01:00" }
///   }
/// }
/// </code>
/// </example>
public static class PresenceRateLimitPolicies
{
    /// <summary>
    /// Policy applied to <c>POST /my/poll</c>. The documented client cadence is 30-60 s,
    /// so this should ceiling at roughly one call every 15 s per user.
    /// Recommended: FixedWindow, 4 requests/min.
    /// </summary>
    public const string Poll = "presence-poll";

    /// <summary>
    /// Policy applied to <c>PUT /my</c> and <c>DELETE /my/override</c>. Bounds
    /// override-toggle floods that would otherwise amplify into the distributed
    /// event bus / Wolverine outbox.
    /// Recommended: FixedWindow, 60 requests/min.
    /// </summary>
    public const string Mutate = "presence-mutate";

    /// <summary>
    /// Policy applied to <c>GET /users/{id}</c> and <c>POST /users/batch</c>.
    /// Bounds enumeration / scraping attempts.
    /// Recommended: SlidingWindow, 30 requests/min.
    /// </summary>
    public const string Query = "presence-query";
}
