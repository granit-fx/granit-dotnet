namespace Granit.Identity;

/// <summary>
/// Computes deterministic lookup digests for encrypted PII columns (the canonical
/// <c>User.Email</c> / <c>User.PhoneNumber</c> and the federated
/// <c>FederatedIdentity.Email</c> mirror) so admin search and exact-match recovery
/// flows can resolve a user without decrypting every row.
/// </summary>
/// <remarks>
/// <para>
/// The plaintext columns are encrypted at rest via
/// <see cref="Granit.Encryption.EncryptedAttribute"/>. AES-CBC ciphertext is
/// non-deterministic (random IV) and cannot serve <c>WHERE col = ?</c>
/// equality lookups; parallel <c>*Hash</c> columns
/// (<c>HMAC-SHA256(pepper, lower-cased value)</c>) are indexed instead.
/// </para>
/// <para>
/// One hasher (and therefore one pepper) is shared by the local and federated
/// stores so a given email produces the same digest on both sides. The pepper
/// (<see cref="Granit.Identity.Options.UserLookupHasherOptions.Pepper"/>) MUST be
/// distinct from the encryption key so the two can be rotated independently;
/// sharing a key would leak re-identification risk into the encryption domain.
/// </para>
/// </remarks>
public interface IUserLookupHasher
{
    /// <summary>Computes the deterministic digest of a user email.</summary>
    string? ComputeEmailHash(string? email);

    /// <summary>Computes the deterministic digest of a user phone number.</summary>
    string? ComputePhoneHash(string? phoneNumber);
}
