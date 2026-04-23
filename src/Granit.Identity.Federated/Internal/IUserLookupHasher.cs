namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Computes a deterministic lookup hash for cache-entry PII so the admin search
/// path can resolve a user by email without decrypting every row.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Granit.Identity.Federated.Domain.UserCacheEntry.Email"/>
/// is encrypted at rest via <see cref="Granit.Encryption.EntityFrameworkCore.EncryptedAttribute"/>.
/// Ciphertext is not <c>LIKE</c>-searchable, so an exact-match lookup index sits
/// alongside the encrypted column: <c>EmailHash = HMAC-SHA256(pepper, lowered-email)</c>.
/// </para>
/// <para>
/// The pepper MUST be distinct from the encryption key so the two can be rotated
/// independently. A shared key would leak re-identification risk into the
/// encryption domain.
/// </para>
/// </remarks>
internal interface IUserLookupHasher
{
    /// <summary>
    /// Computes the deterministic lookup hash for an email address. Returns <c>null</c>
    /// when <paramref name="email"/> is <c>null</c> or empty so the caller can persist
    /// it verbatim.
    /// </summary>
    /// <param name="email">The email address to hash.</param>
    /// <returns>Lower-case hex HMAC digest, or <c>null</c>.</returns>
    string? ComputeEmailHash(string? email);
}
