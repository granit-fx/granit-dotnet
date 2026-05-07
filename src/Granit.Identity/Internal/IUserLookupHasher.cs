namespace Granit.Identity.Internal;

/// <summary>
/// Computes deterministic lookup digests for canonical-user PII columns
/// (<see cref="Granit.Identity.Domain.User.Email"/>,
/// <see cref="Granit.Identity.Domain.User.PhoneNumber"/>) so admin search and
/// future exact-match recovery flows can resolve a user without decrypting
/// every row.
/// </summary>
/// <remarks>
/// <para>
/// The plaintext columns are encrypted at rest via
/// <see cref="Granit.Encryption.EncryptedAttribute"/>. AES-CBC ciphertext is
/// non-deterministic (random IV) and cannot serve <c>WHERE col = ?</c>
/// equality lookups; parallel <c>*Hash</c> columns
/// (<c>HMAC-SHA256(pepper, lower-cased value)</c>) are indexed instead.
/// Same pattern as
/// <see cref="Granit.Identity.Federated.Internal.IUserLookupHasher"/>;
/// each module owns its own hasher so the peppers — and therefore the indexes
/// — can be rotated independently.
/// </para>
/// <para>
/// The pepper MUST be distinct from the encryption key so the two can be
/// rotated independently; sharing a key would leak re-identification risk
/// into the encryption domain.
/// </para>
/// </remarks>
internal interface IUserLookupHasher
{
    /// <summary>Computes the deterministic digest of a user email.</summary>
    string? ComputeEmailHash(string? email);

    /// <summary>Computes the deterministic digest of a user phone number.</summary>
    string? ComputePhoneHash(string? phoneNumber);
}
