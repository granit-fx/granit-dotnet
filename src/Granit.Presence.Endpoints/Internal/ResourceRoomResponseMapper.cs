using Granit.Presence.Abstractions;
using Granit.Presence.Endpoints.Dtos;

namespace Granit.Presence.Endpoints.Internal;

internal static class ResourceRoomResponseMapper
{
    /// <summary>
    /// Projects a <see cref="ResourceRoom"/> to its API response, optionally restricted to
    /// the supplied <paramref name="visibleParticipantIds"/> set (used by the visibility policy).
    /// When <paramref name="visibleParticipantIds"/> is <c>null</c>, every participant is included.
    /// </summary>
    public static ResourceRoomResponse ToResponse(
        ResourceRoom room,
        IReadOnlySet<Guid>? visibleParticipantIds)
    {
        List<ResourcePresenceParticipantResponse> projected = new(room.Participants.Count);
        foreach (ResourcePresenceEntry entry in room.Participants)
        {
            if (visibleParticipantIds?.Contains(entry.UserId) == false)
            {
                continue;
            }

            projected.Add(new ResourcePresenceParticipantResponse(
                entry.UserId,
                entry.LastSeenUtc,
                entry.Metadata));
        }

        return new ResourceRoomResponse(room.Resource.Kind, room.Resource.Id, projected);
    }
}
