using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Encryption;

namespace Granit.Wolverine.Encryption.Internal;

/// <summary>
/// JSON converter that transparently encrypts a <see cref="string"/> property on
/// write and decrypts it on read using <see cref="IStringEncryptionService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Wire format: <c>enc:v1:&lt;cipher&gt;</c> where <c>cipher</c> is whatever the
/// configured <see cref="IStringEncryptionService"/> returns from
/// <see cref="IStringEncryptionService.Encrypt"/> (typically Base64-encoded
/// AES-GCM output). The <c>enc:v1:</c> prefix lets the read path identify
/// peppered values vs legacy plaintext rows already in the outbox / saga
/// store: a value without the prefix is returned verbatim, so existing data
/// remains usable until the row is rotated through the next write.
/// </para>
/// <para>
/// Attached only to properties carrying <see cref="EncryptedAttribute"/> via
/// <see cref="EncryptedPropertyJsonTypeInfoModifier"/>. Properties without the
/// marker are unaffected.
/// </para>
/// </remarks>
internal sealed class EncryptedStringJsonConverter(IStringEncryptionService encryption)
    : JsonConverter<string>
{
    internal const string VersionPrefix = "enc:v1:";

    private readonly IStringEncryptionService _encryption = encryption;

    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        string raw = reader.GetString() ?? string.Empty;

        if (!raw.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            // Legacy plaintext row written before the convention was applied.
            // Return as-is so existing outbox / saga rows remain usable.
            return raw;
        }

        string cipher = raw[VersionPrefix.Length..];
        return _encryption.Decrypt(cipher);
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        string cipher = _encryption.Encrypt(value);
        writer.WriteStringValue(VersionPrefix + cipher);
    }
}
