namespace Granit.Identity;

/// <summary>
/// Durable store for session risk verdicts, keyed by <c>(userId, sessionId)</c>. The single source of truth
/// the BFF and identity session surfaces read their displayed risk level from.
/// </summary>
/// <remarks>
/// <para>
/// The default registration (<c>Granit.Identity.Abstractions</c>) is an in-memory, single-node, non-durable
/// store suitable for development. Install <c>Granit.UserSessions.EntityFrameworkCore</c> for a durable store that
/// survives restarts and is shared across instances.
/// </para>
/// <para>
/// Lifetime contract: implementations must be safe to resolve per request scope. The default in-memory store is
/// registered <c>Singleton</c> (shared state) while the EF Core store is <c>Scoped</c>; consumers must therefore
/// depend on this interface from a scoped (or transient) service — never capture it in a singleton.
/// </para>
/// </remarks>
public interface IUserSessionRiskStore
{
    /// <summary>Stores (or replaces) the verdict for a session.</summary>
    Task SetAsync(
        string userId,
        string sessionId,
        UserSessionRiskVerdict verdict,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the verdict for a single session, or <c>null</c> when none has been recorded.</summary>
    Task<UserSessionRiskVerdict?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads verdicts for many sessions of a user in one call (for list endpoints). Sessions without a recorded
    /// verdict are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, UserSessionRiskVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> sessionIds,
        CancellationToken cancellationToken = default);
}
