using System.Collections.Concurrent;

namespace Granit.Identity.Internal;

/// <summary>
/// In-memory, single-node, non-durable <see cref="IUserSessionReviewStore"/> registered by default. Suitable for
/// development and tests; replaced by <c>Granit.Identity.EntityFrameworkCore</c> for durable production use (the
/// idempotency guarantee would otherwise reset on restart and not span instances).
/// </summary>
internal sealed class MemoryUserSessionReviewStore : IUserSessionReviewStore
{
    private readonly ConcurrentDictionary<(string UserId, string SessionId), UserSessionReviewDecision> _decisions =
        new();

    public Task<UserSessionReviewDecision?> GetDecisionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<UserSessionReviewDecision?>(
            _decisions.TryGetValue((userId, sessionId), out UserSessionReviewDecision decision) ? decision : null);

    public Task<bool> TryRecordDecisionAsync(
        string userId,
        string sessionId,
        UserSessionReviewDecision decision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default) =>
        // TryAdd is atomic: exactly one concurrent caller wins, giving single-use semantics for free.
        Task.FromResult(_decisions.TryAdd((userId, sessionId), decision));
}
