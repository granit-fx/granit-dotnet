using Granit.DataProtection;
using Granit.Domain;
using Granit.Encryption;
using Granit.Privacy.DataDeletion;

namespace Granit.Privacy.EntityFrameworkCore.Entities;

/// <summary>
/// Read-model row persisted by <c>EfDeletionRequestTracker</c> for each deferred personal-data
/// deletion request (GDPR Art. 17 cooling-off period). The state machine lives in
/// <c>PersonalDataDeletionSaga</c>; this entity is the CQRS projection used by
/// <c>GET /privacy/erasure</c>.
/// </summary>
public sealed class DeletionRequestEntity : Entity, IMultiTenant
{
    /// <summary>Data subject whose data is scheduled for deletion.</summary>
    public Guid UserId { get; set; }

    /// <summary>Current lifecycle state of the request.</summary>
    public DeletionRequestState State { get; set; }

    /// <summary>Free-text justification captured from the user — may contain personal data.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    [Encrypted]
    public string Reason { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the user submitted the deletion request.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>UTC timestamp at which providers must actually erase data (end of grace period).</summary>
    public DateTimeOffset ScheduledDeletionAt { get; set; }

    /// <summary>UTC timestamp when the user cancelled within the grace period, or <c>null</c>.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>UTC timestamp when providers completed the erasure, or <c>null</c> while deferred.</summary>
    public DateTimeOffset? ExecutedAt { get; set; }

    /// <summary>Applicable privacy regulation code (e.g., <c>EU_GDPR</c>, <c>BR_LGPD</c>).</summary>
    public string? Regulation { get; set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }
}
