namespace Granit.Bff.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity representing a BFF session with serialized tokens.
/// </summary>
internal sealed class BffSessionEntity
{
    /// <summary>Primary key (auto-generated GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>The session identifier (from the session cookie).</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The frontend name (e.g., "admin", "patient").</summary>
    public string FrontendName { get; set; } = string.Empty;

    /// <summary>The user's subject identifier (sub claim). Null for anonymous sessions.</summary>
    public string? UserId { get; set; }

    /// <summary>JSON-serialized <see cref="BffTokenSet"/>.</summary>
    public string SerializedTokens { get; set; } = string.Empty;

    /// <summary>When this session expires (for cleanup job).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When this session was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When this session was last accessed on the proxy path (throttled). Authoritative for display.</summary>
    public DateTimeOffset? LastAccessedAt { get; set; }

    /// <summary>
    /// Client IP last observed for this session, encrypted at rest when an encryption service is configured.
    /// Authoritative over the value embedded in <see cref="SerializedTokens"/>.
    /// </summary>
    public string? IpAddress { get; set; }
}
