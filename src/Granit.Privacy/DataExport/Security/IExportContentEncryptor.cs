namespace Granit.Privacy.DataExport.Security;

/// <summary>
/// Application-layer authenticated encryption for the assembled Subject Access Request
/// (SAR) package. Companion to <see cref="IExportContentSigner"/>: where the content
/// signer binds an <em>integrity</em> tag over a plaintext manifest, this encryptor
/// provides <em>confidentiality</em> for the whole SAR envelope before it is handed to
/// the blob tier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists (GDPR Art. 32 "state of the art").</b> A completed SAR package is a
/// full copy of a data subject's personal data. Relying on the blob provider's
/// server-side encryption (SSE) alone leaves the plaintext readable by anyone with
/// storage-tier access (provider operators, misconfigured buckets, leaked credentials).
/// Encrypting at the application layer keeps the bytes opaque everywhere below the
/// process boundary — only a holder of the export key can read them.
/// </para>
/// <para>
/// <b>Authenticated encryption.</b> Implementations MUST use an AEAD construction
/// (AES-256-GCM in the framework default) so tampering is detected on decrypt: a flipped
/// ciphertext byte fails the authentication tag <em>before</em> any plaintext is exposed.
/// This subsumes the manifest HMAC for confidentiality-plus-integrity, while the existing
/// <see cref="IExportContentSigner"/> tag still travels inside the (now encrypted) payload
/// for defence in depth and for clients that verify the manifest structure after decrypt.
/// </para>
/// <para>
/// <b>Key handling.</b> The export key is provisioned exactly like the HMAC signer's key
/// (see <see cref="IExportHmacSigner"/> key-management remarks): the in-memory default
/// generates a fresh key per process and is fail-closed outside <c>Development</c>;
/// production hosts register a Vault-backed implementation so the key lives in Vault and
/// never leaves it in plaintext. The data subject's client never receives the raw key —
/// decryption happens server-side on the download path, behind the same subject-identity
/// check and step-up authentication that already gate the download endpoints, and the
/// plaintext is streamed to the subject over the authenticated BFF channel. This keeps
/// the confidentiality boundary at the process, not at the client.
/// </para>
/// <para>
/// <b>Ciphertext format</b> (framework default): <c>[12-byte nonce][16-byte GCM tag][ciphertext]</c>,
/// self-describing so no external nonce store is needed. Provider-backed implementations
/// may use their own envelope as long as decrypt is symmetric.
/// </para>
/// </remarks>
public interface IExportContentEncryptor
{
    /// <summary>
    /// Encrypts the SAR package bytes with the export key, producing a self-describing
    /// authenticated ciphertext.
    /// </summary>
    /// <param name="plaintext">The assembled (signed) SAR envelope bytes.</param>
    /// <returns>Opaque ciphertext safe to upload to the blob tier.</returns>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Decrypts and authenticates a ciphertext produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="ciphertext">The stored ciphertext.</param>
    /// <returns>The original SAR envelope bytes.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when the authentication tag does not validate (tampered or truncated
    /// ciphertext) — no plaintext is returned in that case.
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> ciphertext);
}
