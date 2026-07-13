namespace Granit.Privacy.DataExport.Security;

/// <summary>
/// Signs and verifies arbitrary byte payloads with the export HMAC key — companion
/// to <see cref="IExportHmacSigner"/> which signs fragment identity tuples. Used
/// by the assembly job to seal the manifest sidecar (sha256-per-shard + schema
/// version), and by hosts to detect tampering before trusting a downloaded
/// manifest.
/// </summary>
/// <remarks>
/// <para>
/// Same key material as <see cref="IExportHmacSigner"/> but a distinct signing
/// surface: fragment tags bind identity tuples, manifest tags bind whole JSON
/// payloads. Keeping the two methods on separate interfaces prevents accidental
/// cross-domain verification (a fragment tag never validates against manifest
/// bytes and vice versa, even when the underlying key is shared).
/// </para>
/// <para>
/// <b>Tag format:</b> identical to fragment tags — <c>v{version}:{Base64Url(HMAC-SHA256(K, payload))}</c>.
/// </para>
/// </remarks>
public interface IExportContentSigner
{
    /// <summary>Produces an integrity tag over the canonical byte payload.</summary>
    Task<string> SignBytesAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="tag"/> verifies against
    /// <paramref name="payload"/> under any key version known to the signer.
    /// </summary>
    Task<bool> VerifyBytesAsync(ReadOnlyMemory<byte> payload, string tag, CancellationToken cancellationToken = default);
}
