namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>
/// Computes deterministic lookup digests for party canonical-projection columns
/// (<see cref="Granit.Parties.Domain.PartyEmail.CanonicalEmail"/>,
/// <see cref="Granit.Parties.Domain.PartyPhone.CanonicalNumber"/>) so the Tier-1
/// duplicate matcher can index them without decrypting every row.
/// </summary>
/// <remarks>
/// <para>
/// The canonical columns are encrypted at rest via
/// <see cref="Granit.Encryption.EncryptedAttribute"/>; AES-CBC ciphertext is
/// non-deterministic (random IV) and cannot serve the
/// <c>WHERE canonical IN (…)</c> equality lookups that
/// <see cref="Granit.Parties.Deduplication.Internal.Tier1DeterministicMatcher"/>
/// performs. A parallel <c>*Hash</c> column (<c>HMAC-SHA256(pepper, canonical)</c>)
/// is indexed instead. Same pattern as
/// <see cref="Granit.Identity.Federated.Internal.IUserLookupHasher"/>.
/// </para>
/// <para>
/// The pepper MUST be distinct from the encryption key so the two can be rotated
/// independently. A shared key would leak re-identification risk into the
/// encryption domain.
/// </para>
/// </remarks>
internal interface IPartyLookupHasher
{
    /// <summary>
    /// Computes the deterministic lookup digest of a canonical email or phone
    /// string. Returns <see langword="null"/> when <paramref name="canonical"/>
    /// is <see langword="null"/> or empty so callers can persist <c>null</c>
    /// verbatim alongside a <c>null</c> canonical value.
    /// </summary>
    string? ComputeHash(string? canonical);
}
