namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Resource room snapshot returned by the room endpoints.
/// </summary>
/// <param name="Kind">Resource kind discriminator (echoes the URL segment).</param>
/// <param name="Id">Resource identifier (echoes the URL segment).</param>
/// <param name="Participants">Live participants, filtered by the visibility policy.</param>
public sealed record ResourceRoomResponse(
    string Kind,
    string Id,
    IReadOnlyList<ResourcePresenceParticipantResponse> Participants);

/// <summary>
/// A single participant entry inside a <see cref="ResourceRoomResponse"/>.
/// </summary>
/// <param name="UserId">The participant's user identifier.</param>
/// <param name="LastSeenUtc">Server-side instant of the participant's most recent heartbeat.</param>
/// <param name="Metadata">Optional caller-supplied opaque metadata (≤ 512 bytes UTF-8).</param>
public sealed record ResourcePresenceParticipantResponse(
    Guid UserId,
    DateTimeOffset LastSeenUtc,
    string? Metadata);
