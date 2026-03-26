namespace Granit.DataProtection;

/// <summary>
/// Marks a property as containing sensitive or personal data (GDPR Art. 25 — data minimization).
/// </summary>
/// <remarks>
/// <para>
/// Cross-cutting marker consumed by multiple framework modules:
/// </para>
/// <list type="bullet">
///   <item><b>Auditing</b>: property values masked, omitted, or hashed in the audit trail.</item>
///   <item><b>AI / MCP</b>: property values redacted from LLM tool outputs (OWASP LLM06).</item>
///   <item><b>Logging</b>: property values redacted from structured log fields.</item>
///   <item><b>Data Export</b>: property values flagged for special handling in GDPR data subject exports.</item>
/// </list>
/// <para>
/// The <see cref="Mode"/> property controls <b>how</b> the value is protected.
/// Each consumer module interprets the mode according to its context.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SensitiveData]                             // Default: Mask → "***"
/// public string? Email { get; set; }
///
/// [SensitiveData(Mode = SensitiveDataMode.Omit)]   // Remove entirely from output
/// public string? PasswordHash { get; set; }
///
/// [SensitiveData(Mode = SensitiveDataMode.Hash)]   // SHA-256 for correlation
/// public string? ExternalUserId { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the protection mode applied when this property crosses a trust boundary.
    /// Default: <see cref="SensitiveDataMode.Mask"/>.
    /// </summary>
    public SensitiveDataMode Mode { get; init; } = SensitiveDataMode.Mask;
}
