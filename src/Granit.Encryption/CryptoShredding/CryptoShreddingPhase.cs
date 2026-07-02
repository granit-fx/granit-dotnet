namespace Granit.Encryption.CryptoShredding;

/// <summary>
/// Identifies which phase of the two-phase crypto-shredding sequence an audit record captures.
/// </summary>
/// <remarks>
/// The intent record (<see cref="Requested"/>) is written durably <em>before</em> the irreversible
/// key destruction, so an audit failure can never leave an erasure without a trail (GDPR Art. 5(2)).
/// The confirmation record (<see cref="Confirmed"/>) is written after the key has been destroyed.
/// </remarks>
public enum CryptoShreddingPhase
{
    /// <summary>
    /// The intent to destroy the key, recorded before destruction. A durable trail exists from this
    /// point on, even if the process crashes between phases.
    /// </summary>
    Requested,

    /// <summary>
    /// The key has been destroyed and the erasure is complete.
    /// </summary>
    Confirmed
}
