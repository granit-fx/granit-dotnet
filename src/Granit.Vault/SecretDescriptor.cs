using System.Text;
using Granit.DataProtection;

namespace Granit.Vault;

/// <summary>
/// The payload and metadata returned by <see cref="ISecretStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariant:</b> exactly one of <see cref="StringValue"/> or <see cref="BinaryValue"/>
/// is non-null. Use <see cref="FromString"/> / <see cref="FromBinary"/> to construct
/// instances — these enforce the invariant.
/// </para>
/// <para>
/// <b>Sensitive data:</b> payload fields are annotated with <see cref="SensitiveDataAttribute"/>
/// to be redacted by auditing, logging, and AI/MCP cross-cutting consumers. The
/// <see cref="ToString"/> override additionally redacts the payload for any serializer
/// that bypasses the attribute.
/// </para>
/// </remarks>
public sealed record SecretDescriptor
{
    /// <summary>Provider-addressable secret name (as requested).</summary>
    public required string Name { get; init; }

    /// <summary>Text payload (UTF-8). Non-null iff <see cref="BinaryValue"/> is null.</summary>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    public string? StringValue { get; init; }

    /// <summary>Binary payload. Non-null iff <see cref="StringValue"/> is null.</summary>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    public ReadOnlyMemory<byte>? BinaryValue { get; init; }

    /// <summary>Provider-specific version identifier if available.</summary>
    public string? Version { get; init; }

    /// <summary>Media type hint (e.g. <c>"application/octet-stream"</c>, <c>"text/plain"</c>).</summary>
    public string? ContentType { get; init; }

    /// <summary>Creation timestamp if provider exposes it.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Expiration timestamp if provider exposes it (Azure Key Vault, some HashiCorp configurations).</summary>
    public DateTimeOffset? ExpiresOn { get; init; }

    /// <summary>"Not before" timestamp if provider exposes it (Azure Key Vault).</summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>Provider-exposed key/value tags or labels.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>True when the payload is binary — use <see cref="BinaryValue"/> / <see cref="AsBytes"/>.</summary>
    public bool IsBinary => BinaryValue is not null;

    /// <summary>Builds a descriptor for a text secret. Pass <see cref="SecretMetadata.Empty"/> when no metadata is available.</summary>
    public static SecretDescriptor FromString(string name, string value, SecretMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);

        metadata ??= SecretMetadata.Empty;
        return new SecretDescriptor
        {
            Name = name,
            StringValue = value,
            Version = metadata.Version,
            ContentType = metadata.ContentType,
            CreatedAt = metadata.CreatedAt,
            ExpiresOn = metadata.ExpiresOn,
            NotBefore = metadata.NotBefore,
            Tags = metadata.Tags,
        };
    }

    /// <summary>Builds a descriptor for a binary secret. Pass <see cref="SecretMetadata.Empty"/> when no metadata is available.</summary>
    public static SecretDescriptor FromBinary(string name, ReadOnlyMemory<byte> value, SecretMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        metadata ??= SecretMetadata.Empty;
        return new SecretDescriptor
        {
            Name = name,
            BinaryValue = value,
            Version = metadata.Version,
            ContentType = metadata.ContentType,
            CreatedAt = metadata.CreatedAt,
            ExpiresOn = metadata.ExpiresOn,
            NotBefore = metadata.NotBefore,
            Tags = metadata.Tags,
        };
    }

    /// <summary>
    /// Returns the raw bytes: <see cref="BinaryValue"/> when binary, otherwise UTF-8
    /// encoding of <see cref="StringValue"/>.
    /// </summary>
    public byte[] AsBytes()
    {
        if (BinaryValue is { } bytes)
        {
            return bytes.ToArray();
        }

        if (StringValue is not null)
        {
            return Encoding.UTF8.GetBytes(StringValue);
        }

        throw new InvalidOperationException(
            "SecretDescriptor invariant violated: neither StringValue nor BinaryValue is set.");
    }

    /// <summary>
    /// Returns the string representation: <see cref="StringValue"/> when text,
    /// otherwise Base64 of <see cref="BinaryValue"/>.
    /// </summary>
    public string AsString()
    {
        if (StringValue is not null)
        {
            return StringValue;
        }

        if (BinaryValue is { } bytes)
        {
            return Convert.ToBase64String(bytes.Span);
        }

        throw new InvalidOperationException(
            "SecretDescriptor invariant violated: neither StringValue nor BinaryValue is set.");
    }

    /// <summary>
    /// Redacted member printer — prevents payload leakage through ToString() or
    /// reflection-based serializers that bypass <see cref="SensitiveDataAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Records' auto-generated ToString calls this method; overriding it is the supported
    /// extension point for customizing the textual representation. Replaces the payload
    /// with a length-only placeholder. Verified by <c>VaultConventionTests</c>.
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Name = ").Append(Name);
        builder.Append(", Version = ").Append(Version ?? "<latest>");
        builder.Append(", StringValue = <redacted ").Append(StringValue?.Length ?? 0).Append(" chars>");
        builder.Append(", BinaryValue = <redacted ").Append(BinaryValue?.Length ?? 0).Append(" bytes>");
        builder.Append(", ContentType = ").Append(ContentType);
        builder.Append(", ExpiresOn = ").Append(ExpiresOn);
        return true;
    }
}
