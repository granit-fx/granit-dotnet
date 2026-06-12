using System.Collections.Concurrent;

namespace Granit.Identity.Internal;

/// <summary>
/// In-memory, single-node, non-durable <see cref="IUserSessionRiskStore"/> registered by default. Suitable for
/// development and tests; replaced by <c>Granit.UserSessions.EntityFrameworkCore</c> for durable production use.
/// </summary>
internal sealed class MemoryUserSessionRiskStore : IUserSessionRiskStore
{
    private readonly ConcurrentDictionary<(string UserId, string SessionId), UserSessionRiskVerdict> _verdicts =
        new();

    public Task SetAsync(
        string userId,
        string sessionId,
        UserSessionRiskVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        _verdicts[(userId, sessionId)] = verdict;
        return Task.CompletedTask;
    }

    public Task<UserSessionRiskVerdict?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_verdicts.GetValueOrDefault((userId, sessionId)));

    public Task<IReadOnlyDictionary<string, UserSessionRiskVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> sessionIds,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, UserSessionRiskVerdict> result = [];
        foreach (string sessionId in sessionIds)
        {
            if (_verdicts.TryGetValue((userId, sessionId), out UserSessionRiskVerdict? verdict))
            {
                result[sessionId] = verdict;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, UserSessionRiskVerdict>>(result);
    }
}
