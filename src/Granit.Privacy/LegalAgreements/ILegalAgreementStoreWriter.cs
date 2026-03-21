using Granit.Privacy.LegalAgreements.Domain;

namespace Granit.Privacy.LegalAgreements;

/// <summary>
/// Write-only persistence abstraction for legal agreements. Implemented by the application (EF Core, etc.).
/// </summary>
public interface ILegalAgreementStoreWriter
{
    /// <summary>Records a new legal agreement (append-only).</summary>
    Task RecordAsync(LegalAgreementBase agreement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records consent from primitive values, without requiring a concrete <see cref="LegalAgreementBase"/> subclass.
    /// Used by <c>Granit.Privacy.Endpoints</c> to record consent without depending on the application's entity type.
    /// </summary>
    /// <param name="userId">Identifier of the consenting user.</param>
    /// <param name="documentId">Legal document identifier (e.g., "privacy-policy").</param>
    /// <param name="version">Version of the document accepted (e.g., "2.1.0").</param>
    /// <param name="ipAddress">Pseudonymized IP address (last octet masked), or <c>null</c>.</param>
    /// <param name="acceptedAt">Timestamp of acceptance (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordConsentAsync(
        Guid userId,
        string documentId,
        string version,
        string? ipAddress,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken = default);
}
