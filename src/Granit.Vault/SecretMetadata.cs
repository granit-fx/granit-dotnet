namespace Granit.Vault;

/// <summary>
/// Provider-exposed metadata attached to a secret. All fields are nullable because
/// not every provider surfaces every piece of information.
/// </summary>
/// <param name="Version">Provider-specific version identifier (integer for HashiCorp KV v2,
/// UUID for Azure, GUID for AWS, numeric / "latest" for GCP).</param>
/// <param name="ContentType">Media type hint — <c>"application/octet-stream"</c> signals
/// a binary payload, any other value is informational. Primarily set by Azure Key Vault.</param>
/// <param name="CreatedAt">Creation timestamp. HashiCorp, Azure, AWS expose it; GCP does not.</param>
/// <param name="ExpiresOn">Expiration timestamp. Azure Key Vault exposes this natively —
/// consumers can use it to schedule proactive rotations (e.g. mTLS certificate reload).</param>
/// <param name="NotBefore">"Not before" timestamp. Azure Key Vault only.</param>
/// <param name="Tags">Provider-exposed key/value labels. Azure and AWS support user-defined tags.</param>
public sealed record SecretMetadata(
    string? Version = null,
    string? ContentType = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? ExpiresOn = null,
    DateTimeOffset? NotBefore = null,
    IReadOnlyDictionary<string, string>? Tags = null)
{
    /// <summary>Empty metadata — all fields null. Convenience constant for providers that expose nothing.</summary>
    public static SecretMetadata Empty { get; } = new();
}
