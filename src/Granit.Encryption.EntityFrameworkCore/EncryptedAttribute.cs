namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// Marks a <c>string</c> entity property as encrypted at rest.
/// <para>
/// <see cref="EncryptedStringConverter"/> is applied automatically by
/// <c>ModelBuilderEncryptionExtensions.ApplyEncryptionConventions()</c> during
/// <c>OnModelCreating</c>, transparently calling <see cref="IStringEncryptionService"/>
/// on every read and write.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class Patient : Entity&lt;Guid&gt;
/// {
///     [Encrypted]
///     public string Ssn { get; set; } = string.Empty;
///
///     [Encrypted]
///     public string? MedicalNotes { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute;
