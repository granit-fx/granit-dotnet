using Granit.Domain;

namespace Granit.Privacy.LegalAgreements.Domain;

/// <summary>
/// Abstract base entity for legal agreement records (RGPD Art. 7 — proof of consent).
/// Applications must create a concrete entity inheriting this class and map it in their DbContext.
/// </summary>
/// <remarks>
/// Each record represents a single consent event: which user accepted which document version, when.
/// Records are immutable (append-only) — consent is never updated, only new records are added.
/// </remarks>
public abstract class LegalAgreementBase : CreationAuditedEntity
{
    /// <summary>Identifier of the user who consented.</summary>
    public Guid UserId { get; set; }

    /// <summary>Identifier of the legal document (e.g., "privacy-policy", "terms-of-service").</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Version of the document the user accepted (e.g., "2.1.0").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Timestamp of acceptance (UTC).</summary>
    public DateTimeOffset AcceptedAt { get; set; }

    /// <summary>
    /// IP address of the user at the time of acceptance (pseudonymized — last octet masked, RGPD).
    /// </summary>
    public string? IpAddress { get; set; }
}
