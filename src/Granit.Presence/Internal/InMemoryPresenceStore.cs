using System.Collections.Concurrent;
using Granit.Presence.Abstractions;
using Granit.Presence.Domain;

namespace Granit.Presence.Internal;

/// <summary>
/// Default thread-safe in-memory implementation of <see cref="IPresenceStore"/>.
/// Replaced at registration time by the EF Core store when <c>Granit.Presence.EntityFrameworkCore</c>
/// is loaded.
/// </summary>
internal sealed class InMemoryPresenceStore : IPresenceStore
{
    private readonly ConcurrentDictionary<Guid, UserPresence> _byUserId = new();

    public Task<UserPresence?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        _byUserId.TryGetValue(userId, out UserPresence? value);
        return Task.FromResult(value);
    }

    public Task<IReadOnlyDictionary<Guid, UserPresence>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        Dictionary<Guid, UserPresence> result = [];
        foreach (Guid userId in userIds)
        {
            if (_byUserId.TryGetValue(userId, out UserPresence? value))
            {
                result[userId] = value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, UserPresence>>(result);
    }

    public Task<UserPresence> MutateAsync(
        Guid userId,
        Func<UserPresence> factory,
        Action<UserPresence> mutator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(mutator);

        UserPresence presence = _byUserId.GetOrAdd(userId, _ => factory());
        mutator(presence);
        _byUserId[userId] = presence;
        return Task.FromResult(presence);
    }

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        _byUserId.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
