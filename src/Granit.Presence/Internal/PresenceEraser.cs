using Granit.Presence.Abstractions;

namespace Granit.Presence.Internal;

internal sealed class PresenceEraser(IPresenceStore store, IPresenceTracker tracker) : IPresenceEraser
{
    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken)
    {
        await store.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);
        await tracker.RemoveAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
