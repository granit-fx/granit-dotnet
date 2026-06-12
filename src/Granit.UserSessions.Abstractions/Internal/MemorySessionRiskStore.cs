using System.Collections.Concurrent;

namespace Granit.UserSessions.Internal;

/// <summary>
/// In-memory, single-node, non-durable <see cref="ISessionRiskStore"/> registered by default. Suitable for
/// development and tests; replaced by <c>Granit.UserSessions.EntityFrameworkCore</c> for durable production use.
/// </summary>
internal sealed class MemorySessionRiskStore : ISessionRiskStore
{
    private readonly ConcurrentDictionary<(string UserId, string SessionId), SessionRiskVerdict> _verdicts =
        new();

    public Task SetAsync(
        string userId,
        string sessionId,
        SessionRiskVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        _verdicts[(userId, sessionId)] = verdict;
        return Task.CompletedTask;
    }

    public Task<SessionRiskVerdict?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_verdicts.GetValueOrDefault((userId, sessionId)));

    public Task<IReadOnlyDictionary<string, SessionRiskVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> sessionIds,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, SessionRiskVerdict> result = [];
        foreach (string sessionId in sessionIds)
        {
            if (_verdicts.TryGetValue((userId, sessionId), out SessionRiskVerdict? verdict))
            {
                result[sessionId] = verdict;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, SessionRiskVerdict>>(result);
    }
}
