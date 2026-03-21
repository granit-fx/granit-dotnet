using Granit.Core.Domain;

namespace Granit.OpenIddict.Domain;

/// <summary>
/// Persisted signing or encryption key for the OpenIddict server.
/// </summary>
/// <remarks>
/// <para>
/// Keys are stored encrypted in the database via <c>IStringEncryptionService</c>.
/// The key rotation job generates new keys before the active key expires, and old
/// keys remain in <see cref="SigningKeyStatus.Retired"/> status during the grace period
/// so that tokens signed with them can still be validated.
/// </para>
/// <para>
/// The <c>/.well-known/jwks</c> endpoint exposes all <see cref="SigningKeyStatus.Active"/>
/// and <see cref="SigningKeyStatus.Retired"/> public keys.
/// </para>
/// </remarks>
public class SigningKey : CreationAuditedEntity
{
    /// <summary>The key identifier (kid), used in JWT headers to identify the signing key.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>The key type: <c>"signing"</c> or <c>"encryption"</c>.</summary>
    public string KeyType { get; set; } = "signing";

    /// <summary>The algorithm (e.g., <c>"RS256"</c>, <c>"RS384"</c>, <c>"RSA-OAEP"</c>).</summary>
    public string Algorithm { get; set; } = "RS256";

    /// <summary>The serialized key material (encrypted via IStringEncryptionService).</summary>
#pragma warning disable GRSEC003 // Key material property, not a hardcoded secret
    public string EncryptedKeyMaterial { get; set; } = string.Empty;
#pragma warning restore GRSEC003

    /// <summary>The current status of this key.</summary>
    public SigningKeyStatus Status { get; set; } = SigningKeyStatus.Active;

    /// <summary>When this key becomes active (UTC).</summary>
    public DateTimeOffset ActivatedAt { get; set; }

    /// <summary>When this key expires and should be rotated (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When this key was retired (UTC). Null if still active.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    /// <summary>The RSA key size in bits (e.g., 2048, 4096).</summary>
    public int KeySize { get; set; } = 2048;
}

/// <summary>
/// Status of a signing key in its lifecycle.
/// </summary>
public enum SigningKeyStatus
{
    /// <summary>Key is actively used for signing new tokens.</summary>
    Active = 0,

    /// <summary>Key is retired but still valid for verification (grace period).</summary>
    Retired = 1,

    /// <summary>Key is revoked and should not be used for anything.</summary>
    Revoked = 2,
}
