using Granit.Privacy.DataExport.Fragments;

namespace Granit.Privacy.DataExport.Security;

/// <summary>
/// Signs and verifies the HMAC capability carried by every <see cref="ExportFragment"/>.
/// Closes <c>VULN-001</c> (Broken Object-Level Authorization on PassThrough fragments) and
/// <c>VULN-102</c> (Wolverine outbox message tampering): an attacker (or buggy provider)
/// cannot reference a blob the subject does not own — the archive assembler verifies the
/// tag before opening the source stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tag format:</b> <c>v{version}:{Base64Url(HMAC-SHA256(K, canonical))}</c> where
/// <c>canonical</c> is the deterministic concatenation of fragment identity fields
/// (request id, subject, provider, fragment kind, source blob, entry path, expiry).
/// </para>
/// <para>
/// <b>Key management:</b> the default in-memory implementation
/// (<see cref="EphemeralExportHmacSigner"/>) is suitable for development and tests only.
/// Production hosts MUST register a Vault-backed implementation so the key survives process
/// restarts and rotates on a 90-day schedule (deferred to a follow-up story; the interface
/// is shaped to make that swap a single DI line).
/// </para>
/// </remarks>
public interface IExportHmacSigner
{
    /// <summary>
    /// Produces an integrity tag binding the fragment identity to the signer's current key.
    /// </summary>
    /// <param name="parameters">Identity inputs (deterministic order — see
    /// <see cref="ExportHmacParameters"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The opaque versioned tag to store in
    /// <see cref="ExportFragment.IntegrityTag"/>.</returns>
    Task<string> SignAsync(ExportHmacParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="tag"/> verifies under any key
    /// version known to the signer (supports rolling rotation).
    /// </summary>
    Task<bool> VerifyAsync(ExportHmacParameters parameters, string tag, CancellationToken cancellationToken = default);
}

/// <summary>
/// Input parameters bound by the HMAC capability. Field order matters — the signer
/// canonicalizes by concatenating in declaration order, so changes break existing tags
/// (rolling keys handle that during the migration window).
/// </summary>
/// <param name="RequestId">Export request correlation id.</param>
/// <param name="SubjectUserId">Data subject the fragment belongs to.</param>
/// <param name="ProviderName">Originating provider's <see cref="IPrivacyDataProvider.ProviderName"/>.</param>
/// <param name="FragmentKind">Concrete <see cref="ExportFragment"/> kind
/// (<c>"staged"</c> or <c>"passthrough"</c>) — prevents cross-kind tag substitution.</param>
/// <param name="SourceContainer">Container name of the underlying blob (staged or source).</param>
/// <param name="SourceBlobId">Blob id of the underlying blob.</param>
/// <param name="EntryPath">Path the fragment occupies inside the export archive.</param>
/// <param name="ExpiresAt">Absolute UTC time after which the tag is no longer accepted
/// (replay protection). Typically <c>RequestedAt + ExportTimeoutMinutes + max-assembly-duration</c>.</param>
public readonly record struct ExportHmacParameters(
    Guid RequestId,
    Guid SubjectUserId,
    string ProviderName,
    string FragmentKind,
    string SourceContainer,
    Guid SourceBlobId,
    string EntryPath,
    DateTimeOffset ExpiresAt);
