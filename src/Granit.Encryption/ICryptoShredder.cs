namespace Granit.Encryption;

/// <summary>
/// Permanently destroys per-entity encryption keys, making all ciphertext
/// encrypted with those keys permanently unreadable (GDPR Art. 17 compliance).
/// </summary>
/// <remarks>
/// Crypto-shredding is irreversible — there is no key recovery path.
/// Database rows are preserved (audit trail / legal hold compatibility),
/// but the encrypted fields become unreadable garbage.
/// </remarks>
public interface ICryptoShredder
{
    /// <summary>
    /// Permanently destroys the per-entity encryption key for a single entity.
    /// </summary>
    /// <param name="entityType">Logical entity type name (e.g., <c>"Patient"</c>).</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ShredAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently destroys per-entity encryption keys for multiple entities
    /// of the same type. Used for bulk GDPR erasure.
    /// </summary>
    /// <param name="entityType">Logical entity type name.</param>
    /// <param name="entityIds">Entity identifiers to shred.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ShredBatchAsync(
        string entityType,
        IEnumerable<string> entityIds,
        CancellationToken cancellationToken = default);
}
