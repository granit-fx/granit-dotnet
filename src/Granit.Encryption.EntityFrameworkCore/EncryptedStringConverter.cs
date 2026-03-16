using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// EF Core <see cref="ValueConverter{TModel,TProvider}"/> that transparently encrypts
/// <c>string</c> properties on write and decrypts them on read using
/// <see cref="IStringEncryptionService"/>.
/// <para>
/// Applied automatically by
/// <c>ModelBuilderEncryptionExtensions.ApplyEncryptionConventions()</c> to every
/// property annotated with <see cref="EncryptedAttribute"/>.
/// </para>
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    /// <summary>
    /// Initializes a new instance of <see cref="EncryptedStringConverter"/>.
    /// </summary>
    /// <param name="encryptionService">
    /// The encryption service used for all conversions.
    /// Captured in a closure — must be a singleton or scoped service consistent
    /// with the <c>DbContext</c> lifetime.
    /// </param>
    public EncryptedStringConverter(IStringEncryptionService encryptionService)
        : base(
            plainText => encryptionService.Encrypt(plainText),
            cipherText => encryptionService.Decrypt(cipherText) ?? string.Empty)
    {
    }
}
