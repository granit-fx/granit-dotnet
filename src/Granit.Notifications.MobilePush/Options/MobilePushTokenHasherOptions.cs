namespace Granit.Notifications.MobilePush.Options;

/// <summary>
/// Configuration for the device-token lookup hasher. The pepper is a HMAC key
/// distinct from the encryption key; rotating it invalidates the index and
/// requires a backfill — schedule rotations during a maintenance window.
/// </summary>
public sealed class MobilePushTokenHasherOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:MobilePush:TokenHasher";

    /// <summary>
    /// Gets or sets the HMAC-SHA256 pepper for the device-token lookup hash.
    /// MUST be at least 32 bytes of high-entropy random data (e.g.
    /// <c>openssl rand -hex 32</c>) and stored separately from the encryption
    /// key ring. Hex-encoded values are auto-decoded; everything else is
    /// taken as raw UTF-8.
    /// </summary>
    public string? DeviceTokenLookupPepper { get; set; }
}
