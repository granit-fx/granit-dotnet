using Granit.Domain;

namespace Granit.Auditing.Domain;

/// <summary>
/// Root audit trail entry for ISO 27001 compliance.
/// Records who did what, when, from where, and on which entities.
/// </summary>
/// <remarks>
/// Inherits <see cref="CreationAuditedEntity"/> for Id, CreatedAt, and CreatedBy
/// (auto-populated by <c>AuditedEntityInterceptor</c>). Audit entries are immutable —
/// no modification audit fields needed.
/// </remarks>
public class AuditEntry : CreationAuditedEntity, IMultiTenant
{
    /// <summary>Operation timestamp (UTC).</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Identifier of the user who performed the operation.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the user (nullable for pseudonymization support).</summary>
    public string? UserName { get; set; }

    /// <summary>Audit log category for retention and filtering.</summary>
    public AuditCategory Category { get; set; }

    /// <summary>Source IP address of the request.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Client User-Agent header.</summary>
    public string? UserAgent { get; set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }

    /// <summary>Distributed tracing correlation identifier.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Entity changes recorded in this audit entry.</summary>
    public ICollection<AuditEntityChange> EntityChanges { get; set; } = [];
}
