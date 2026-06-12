namespace Granit.UserSessions.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity persisting a <see cref="UserSessionRiskVerdict"/> for a session, keyed by
/// <c>(UserId, SessionId)</c>.
/// </summary>
internal sealed class UserSessionRiskEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Subject the session belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Session identifier.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Risk level name (PascalCase, for SQL readability).</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>JSON-serialized reason codes.</summary>
    public string ReasonsJson { get; set; } = "[]";

    /// <summary>When the verdict was produced.</summary>
    public DateTimeOffset AssessedAt { get; set; }
}
