namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Heartbeat payload for a resource room.
/// </summary>
/// <param name="Metadata">
/// Optional opaque, caller-supplied UTF-8 string (≤ 512 bytes). Typically a JSON blob the UI
/// uses to render a colored cursor / section indicator next to the participant. Pass <c>null</c>
/// to clear any previously recorded metadata.
/// </param>
public sealed record HeartbeatRoomRequest(string? Metadata = null);
