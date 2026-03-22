namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Action taken by a data provider when handling a personal data deletion request.
/// Used for audit trail (ISO 27001 compliance).
/// </summary>
public enum DeletionAction
{
    /// <summary>Data was permanently removed from the database.</summary>
    PhysicalDelete = 0,

    /// <summary>Data was soft-deleted via <c>ISoftDeletable</c>.</summary>
    SoftDelete = 1,

    /// <summary>PII was replaced with pseudonymized values (ISO 27001 retention — data preserved, identity removed).</summary>
    Anonymized = 2,

    /// <summary>Data was retained as-is due to a legal obligation (e.g., ISO 27001 20-year retention).</summary>
    Retained = 3,

    /// <summary>Combination of multiple actions (e.g., PII anonymized + medical data retained).</summary>
    Mixed = 4,

    /// <summary>Per-entity encryption key was permanently destroyed — ciphertext is mathematically unreadable (GDPR Art. 17 crypto-shredding).</summary>
    CryptoShredding = 5
}
