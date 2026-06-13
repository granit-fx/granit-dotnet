namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity recording a single, single-use "was this you?" decision for a session, keyed by
/// <c>(UserId, SessionId)</c>. The unique index makes the first insert the authoritative one; a concurrent or
/// repeat attempt fails it, giving idempotent single-use semantics.
/// </summary>
internal sealed class UserSessionReviewEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Subject who reviewed the session.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The reviewed session.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The recorded decision. Persisted as its PascalCase name (for SQL-audit readability) by the Granit
    /// enum-as-string convention applied via <c>ApplyGranitConventions</c>.
    /// </summary>
    public UserSessionReviewDecision Decision { get; set; }

    /// <summary>When the decision was recorded.</summary>
    public DateTimeOffset ReviewedAt { get; set; }
}
