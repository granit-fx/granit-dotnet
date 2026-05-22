namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Polling heartbeat payload.
/// </summary>
/// <param name="IdleSeconds">
/// Client-reported idle duration, in seconds, since the last keyboard / mouse / scroll event.
/// MUST be non-negative. Implementations clamp at <c>2 × OfflineThreshold</c> to defend against
/// malicious or buggy clients. The server reconstructs <c>LastActivityUtc</c> using its own
/// clock to avoid client clock-skew issues.
/// </param>
public sealed record HeartbeatRequest(int IdleSeconds);
