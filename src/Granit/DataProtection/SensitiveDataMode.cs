namespace Granit.DataProtection;

/// <summary>
/// Specifies how sensitive data should be protected when crossing a trust boundary
/// (audit trail, MCP tool output, logs, AI context, data export).
/// </summary>
public enum SensitiveDataMode
{
    /// <summary>
    /// Replace the value with a fixed mask (<c>"***"</c>).
    /// Default — suitable for most PII (names, emails, addresses).
    /// </summary>
    Mask,

    /// <summary>
    /// Remove the property entirely from the output.
    /// Use for secrets, passwords, tokens — values that should never leave the system boundary.
    /// </summary>
    Omit,

    /// <summary>
    /// Replace the value with a one-way SHA-256 hash.
    /// Preserves correlation capability (same input → same hash) without exposing the actual value.
    /// Use for identifiers that consumers need to group or deduplicate (e.g., external user IDs).
    /// </summary>
    Hash,
}
