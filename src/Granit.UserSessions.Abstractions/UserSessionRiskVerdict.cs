namespace Granit.UserSessions;

/// <summary>
/// A persisted risk verdict for a session, read back by the session surfaces to display a stable risk level.
/// </summary>
/// <remarks>
/// Persisting the verdict (rather than recomputing or caching volatilely) keeps the displayed risk consistent
/// across application restarts and pod evictions.
/// </remarks>
/// <param name="Level">The classified risk level.</param>
/// <param name="Reasons">Machine-readable reason codes that contributed.</param>
/// <param name="AssessedAt">When the verdict was produced.</param>
public sealed record UserSessionRiskVerdict(
    UserSessionRiskLevel Level,
    IReadOnlyList<string> Reasons,
    DateTimeOffset AssessedAt);
