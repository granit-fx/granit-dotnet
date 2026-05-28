namespace Granit.Vault;

/// <summary>
/// Provider-agnostic MAC (Message Authentication Code) primitive backed by a vault.
/// Implemented by provider-specific packages — HashiCorp Vault Transit
/// (<c>transit/hmac</c>), AWS KMS (<c>GenerateMac</c>/<c>VerifyMac</c>), GCP Cloud KMS
/// (<c>MacSign</c>/<c>MacVerify</c>), Azure Managed HSM (HS256), or the portable
/// <c>SecretBackedMacService</c> that pulls a key from <see cref="ISecretStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tag opacity:</b> the <see cref="TransitMacResult.Mac"/> string is opaque to callers.
/// Implementations choose their own format (e.g. <c>vault:vN:...</c> for HashiCorp,
/// alias-prefixed Base64 for AWS) — the only contract is that <see cref="VerifyAsync"/>
/// accepts the strings produced by <see cref="MacAsync"/>. Parsing the string outside
/// the implementing provider is forbidden (enforced by <c>VaultConventionTests</c>).
/// </para>
/// <para>
/// <b>Rotation window:</b> <see cref="VerifyAsync"/> MUST accept tags signed under any
/// key version still inside the provider's rotation window. Implementations that lack
/// native versioning (AWS KMS) achieve this by configuring both a <c>current</c> and a
/// <c>previous</c> alias and trying both. Versioned providers (HashiCorp, GCP, Azure)
/// honour the provider-native <c>min_decryption_version</c> / enabled-versions semantics.
/// </para>
/// </remarks>
public interface ITransitMacService
{
    /// <summary>
    /// Produces an integrity tag binding <paramref name="input"/> to the signer's current key.
    /// </summary>
    /// <param name="keyName">Logical key name registered in the vault.</param>
    /// <param name="input">Raw payload bytes (deterministic — the caller is responsible for canonicalization).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The opaque MAC tag plus the key version that signed it.</returns>
    Task<TransitMacResult> MacAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="mac"/> verifies under any key
    /// version known to the implementation (supports rolling rotation — see remarks on
    /// <see cref="ITransitMacService"/>).
    /// </summary>
    /// <param name="keyName">Logical key name registered in the vault.</param>
    /// <param name="input">Raw payload bytes — must be byte-identical to the value passed to <see cref="MacAsync"/>.</param>
    /// <param name="mac">The opaque MAC string previously produced by <see cref="MacAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> VerifyAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        string mac,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of <see cref="ITransitMacService.MacAsync"/>.
/// </summary>
/// <param name="Mac">Opaque tag — callers MUST NOT parse this string.</param>
/// <param name="KeyVersion">
/// Version of the key that produced the tag. Surfaced for diagnostics and to let
/// callers age out tags older than a chosen rolling window. <c>0</c> when the
/// implementation has no notion of key version (rare).
/// </param>
public readonly record struct TransitMacResult(string Mac, int KeyVersion);
