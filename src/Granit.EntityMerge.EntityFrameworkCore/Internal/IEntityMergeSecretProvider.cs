namespace Granit.EntityMerge.EntityFrameworkCore.Internal;

/// <summary>
/// Supplies a deployment-bound 32-byte MAC key used by the merge orchestrator to:
/// <list type="bullet">
///   <item>derive the request-hash HMAC stored in <c>merge_idempotency.RequestHash</c>
///   (so a write-only DB compromise cannot forge a replay-poisoning row),</item>
///   <item>derive the result-payload HMAC stored in <c>merge_idempotency.ResultMac</c>
///   (encrypt-then-MAC integrity check verified before deserialisation).</item>
/// </list>
/// The default implementation (<see cref="StringEncryptionEntityMergeSecretProvider"/>) derives
/// the key from <c>IStringEncryptionService.Encrypt</c> over a stable constant — binds the
/// MAC key to whichever encryption provider the host configured (Vault transit, AES static
/// key, etc.) without introducing a new secret-management surface.
/// </summary>
internal interface IEntityMergeSecretProvider
{
    /// <summary>
    /// Returns the 32-byte MAC key. Implementations cache the result — derivation is one-shot.
    /// </summary>
    byte[] GetMacKey();
}
