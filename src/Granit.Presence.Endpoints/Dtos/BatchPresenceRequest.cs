namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Request payload to fetch presence snapshots for many users in a single call.
/// </summary>
/// <param name="UserIds">User identifiers to look up. Must be non-empty and within the configured batch limit.</param>
public sealed record BatchPresenceRequest(IReadOnlyList<Guid> UserIds);
