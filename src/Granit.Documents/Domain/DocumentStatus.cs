namespace Granit.Documents.Domain;

/// <summary>
/// Lifecycle status of a <see cref="Document"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Trashed"/> is a soft-delete via domain status — the row remains and is
/// retained for the configured trash period before the empty-trash background job
/// (F8 / F9.2) promotes it to <see cref="PermanentlyDeleted"/>.
/// </para>
/// <para>
/// <see cref="PermanentlyDeleted"/> is a tombstone retained for the GDPR / ISO 27001
/// audit trail (3 years per <c>BlobStorage</c>'s retention policy). The bytes have
/// been removed from <c>Granit.BlobStorage</c>; only the audit row stays.
/// </para>
/// </remarks>
public enum DocumentStatus
{
    /// <summary>The document is active and visible in browse / search.</summary>
    Active,

    /// <summary>The document has been moved to the trash; awaiting restore or permanent deletion.</summary>
    Trashed,

    /// <summary>Permanent-deletion tombstone — the underlying blobs are gone, the row remains for audit.</summary>
    PermanentlyDeleted,
}
