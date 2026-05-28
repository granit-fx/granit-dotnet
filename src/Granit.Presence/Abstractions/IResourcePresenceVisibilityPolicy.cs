namespace Granit.Presence.Abstractions;

/// <summary>
/// Authorises read access to a <see cref="ResourceRoom"/> and filters its participants. The
/// counterpart to <see cref="IPresenceVisibilityPolicy"/> for the resource-scoped surface —
/// rooms are referenced by an opaque (kind, id) pair the framework cannot independently
/// authorise, so the host application MUST provide this seam in any deployment where rooms
/// are not freely readable.
/// </summary>
/// <remarks>
/// <para>
/// The framework ships <see cref="Internal.AllowAllResourcePresenceVisibilityPolicy"/> as a
/// permissive default. Multi-tenant deployments — or any host where room kinds carry their
/// own authorisation model — MUST register a replacement before the endpoints are mapped.
/// </para>
/// <para>
/// Failed authorisation surfaces as <c>404 Not Found</c> at the endpoint layer (not 403) to
/// avoid leaking room existence — same rationale as user-presence reads.
/// </para>
/// <para>
/// This policy is consulted on <b>read</b> operations only. <c>JoinAsync</c> is a self-action
/// guarded by the <c>Presence.Rooms.Join</c> route permission.
/// </para>
/// </remarks>
public interface IResourcePresenceVisibilityPolicy
{
    /// <summary>Returns <c>true</c> when the caller is allowed to observe the room at all.</summary>
    /// <param name="callerUserId">
    /// Authenticated caller's user identifier, or <see cref="Guid.Empty"/> when unresolved.
    /// </param>
    /// <param name="resource">The room identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> CanReadRoomAsync(
        Guid callerUserId,
        ResourceRef resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filters the room's participant list down to those visible to the caller. Implementations
    /// may use this to hide users in other tenants / outside the caller's collaboration scope
    /// even when the room itself is readable.
    /// </summary>
    /// <param name="callerUserId">
    /// Authenticated caller's user identifier, or <see cref="Guid.Empty"/> when unresolved.
    /// </param>
    /// <param name="resource">The room identifier.</param>
    /// <param name="participantUserIds">The room's current participant user identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlySet<Guid>> FilterVisibleParticipantsAsync(
        Guid callerUserId,
        ResourceRef resource,
        IReadOnlyCollection<Guid> participantUserIds,
        CancellationToken cancellationToken);
}
