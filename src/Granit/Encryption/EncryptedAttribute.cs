namespace Granit.Encryption;

/// <summary>
/// Marks a <c>string</c> property as encrypted at rest.
/// </summary>
/// <remarks>
/// <para>
/// Pure marker — lives in the base <c>Granit</c> package so domain modules can
/// classify their PII fields without taking a hard dependency on
/// <c>Granit.Encryption.EntityFrameworkCore</c>. The actual encryption is wired
/// by <c>ModelBuilderEncryptionExtensions.ApplyEncryptionConventions()</c>
/// (EF Core path) or by the equivalent Wolverine convention (saga / envelope
/// path), both of which scan the model for this attribute.
/// </para>
/// <para>
/// When <see cref="KeyIsolation"/> is <c>true</c>, each entity instance gets
/// its own encryption key (stored in Vault KV), enabling crypto-shredding
/// (GDPR Art. 17) by deleting the per-entity key via <c>ICryptoShredder</c>.
/// </para>
/// </remarks>
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
    /// Default: <c>false</c> (shared key ring via the configured string
    /// encryption service).
    /// </summary>
    public bool KeyIsolation { get; set; }
}
