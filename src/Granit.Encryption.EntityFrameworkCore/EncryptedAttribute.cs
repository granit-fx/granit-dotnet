namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// Marks a <c>string</c> entity property as encrypted at rest.
/// <para>
/// <see cref="EncryptedStringConverter"/> is applied automatically by
/// <c>ModelBuilderEncryptionExtensions.ApplyEncryptionConventions()</c> during
/// <c>OnModelCreating</c>, transparently calling <see cref="IStringEncryptionService"/>
/// on every read and write.
/// </para>
/// <para>
/// When <see cref="KeyIsolation"/> is <c>true</c>, each entity instance gets its own
/// encryption key (stored in Vault KV), enabling crypto-shredding (GDPR Art. 17)
/// by deleting the per-entity key via <see cref="ICryptoShredder"/>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class Patient : Entity&lt;Guid&gt;
/// {
///     [Encrypted]
///     public string Ssn { get; set; } = string.Empty;
///
///     [Encrypted(KeyIsolation = true)]
///     public string? MedicalNotes { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute
{
    /// <summary>
    /// When <c>true</c>, each entity instance gets its own encryption key,
    /// enabling crypto-shredding (GDPR Art. 17) by deleting the per-entity key.
    /// Default: <c>false</c> (shared key ring via <see cref="IStringEncryptionService"/>).
    /// </summary>
    public bool KeyIsolation { get; set; }
}
