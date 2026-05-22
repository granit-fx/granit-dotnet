namespace Granit.Presence.Abstractions;

/// <summary>
/// Decides whether a caller may read another user's presence. Provides the
/// cross-tenant access-control seam for the presence query endpoints, since
/// the <c>UserPresence</c> aggregate is global per user and carries no tenant.
/// </summary>
/// <remarks>
/// <para>
/// The framework ships a permissive default (<see cref="Internal.AllowAllPresenceVisibilityPolicy"/>)
/// that allows any authenticated caller holding <c>Presence.Users.Read</c> to read any
/// snapshot — a deliberate fail-open default so single-tenant or homogeneous deployments
/// require no configuration. <b>Multi-tenant deployments MUST register a tenant-aware
/// implementation</b> (typically driven by <c>Granit.Identity</c> membership lookups)
/// to prevent cross-tenant disclosure.
/// </para>
/// <para>
/// Callers requesting their own presence are exempted at the endpoint layer and never
/// reach this policy.
/// </para>
/// </remarks>
public interface IPresenceVisibilityPolicy
{
    /// <summary>
    /// Returns the subset of <paramref name="targetUserIds"/> the caller is allowed to read.
    /// Targets denied here are silently omitted from batch responses (preferred over 403
    /// to avoid leaking an enumeration channel).
    /// </summary>
    /// <param name="callerUserId">
    /// The authenticated caller's user identifier, or <see cref="Guid.Empty"/> when the
    /// caller's identity could not be resolved.
    /// </param>
    /// <param name="targetUserIds">User identifiers the caller wants to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlySet<Guid>> FilterVisibleAsync(
        Guid callerUserId,
        IReadOnlyCollection<Guid> targetUserIds,
        CancellationToken cancellationToken);
}
