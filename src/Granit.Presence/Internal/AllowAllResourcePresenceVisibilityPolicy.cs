using Granit.Presence.Abstractions;

namespace Granit.Presence.Internal;

/// <summary>
/// Permissive default <see cref="IResourcePresenceVisibilityPolicy"/>: every caller may read
/// every room and see every participant. Suitable only for single-tenant or fully-public
/// deployments — multi-tenant hosts MUST replace this.
/// </summary>
internal sealed class AllowAllResourcePresenceVisibilityPolicy : IResourcePresenceVisibilityPolicy
{
    public Task<bool> CanReadRoomAsync(
        Guid callerUserId,
        ResourceRef resource,
        CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<IReadOnlySet<Guid>> FilterVisibleParticipantsAsync(
        Guid callerUserId,
        ResourceRef resource,
        IReadOnlyCollection<Guid> participantUserIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        IReadOnlySet<Guid> visible = participantUserIds is HashSet<Guid> hs ? hs : [.. participantUserIds];
        return Task.FromResult(visible);
    }
}
