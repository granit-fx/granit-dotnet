namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Batch presence response keyed by user identifier.
/// </summary>
/// <param name="Presences">Snapshot for each requested user.</param>
public sealed record BatchPresenceResponse(IReadOnlyDictionary<Guid, PresenceResponse> Presences);
