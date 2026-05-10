namespace Granit.Parties.EntityFrameworkCore.Options;

/// <summary>
/// Configuration for the party canonical-projection lookup hasher. The pepper
/// is an HMAC key distinct from the encryption key; rotating it invalidates
/// the email / phone duplicate-detection indexes and requires a backfill —
/// schedule rotations during a maintenance window.
/// </summary>
public sealed class PartyLookupHasherOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Parties:LookupHasher";

    /// <summary>
    /// Gets or sets the HMAC-SHA256 pepper for canonical email / phone lookup
    /// digests. MUST be at least 32 bytes of high-entropy random data (e.g.
    /// <c>openssl rand -hex 32</c>) and stored separately from the encryption
    /// key ring. Hex-encoded values are auto-decoded; everything else is taken
    /// as raw UTF-8.
    /// </summary>
    public string? Pepper { get; set; }
}
