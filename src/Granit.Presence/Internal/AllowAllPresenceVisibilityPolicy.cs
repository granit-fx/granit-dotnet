using Granit.Presence.Abstractions;

namespace Granit.Presence.Internal;

/// <summary>
/// Default <see cref="IPresenceVisibilityPolicy"/> that allows the caller to read every
/// requested target. Suitable only for single-tenant deployments. Multi-tenant apps
/// MUST replace this with a tenant-aware implementation.
/// </summary>
internal sealed class AllowAllPresenceVisibilityPolicy : IPresenceVisibilityPolicy
{
    public Task<IReadOnlySet<Guid>> FilterVisibleAsync(
        Guid callerUserId,
        IReadOnlyCollection<Guid> targetUserIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetUserIds);
        IReadOnlySet<Guid> visible = targetUserIds is HashSet<Guid> hs ? hs : [.. targetUserIds];
        return Task.FromResult(visible);
    }
}
