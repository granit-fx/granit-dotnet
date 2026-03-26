namespace Granit.DataProtection;

/// <summary>
/// Marks a property as containing sensitive or personal data (GDPR Art. 25 — data minimization,
/// ISO 27001 A.8.2 — information classification).
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
/// <see cref="Level"/> classifies <b>how sensitive</b> the data is (ISO 27001 A.8.2).
/// Consumers use the level to apply context-dependent thresholds.
/// </para>
/// <para>
/// <see cref="Mode"/> controls <b>how</b> the value is protected when it crosses a trust boundary.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SensitiveData]                                                    // Internal + Mask (default)
/// public string? FirstName { get; set; }
///
/// [SensitiveData(Level = Sensitivity.Confidential)]                  // Confidential + Mask
/// public string? Email { get; set; }
///
/// [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]  // Restricted + Omit
/// public string? PasswordHash { get; set; }
///
/// [SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash)] // Confidential + Hash
/// public string? ExternalUserId { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>
    /// Classification level of the sensitive data (ISO 27001 A.8.2).
    /// Default: <see cref="Sensitivity.Internal"/>.
    /// </summary>
    public Sensitivity Level { get; init; } = Sensitivity.Internal;

    /// <summary>
    /// Protection mode applied when this property crosses a trust boundary.
    /// Default: <see cref="SensitiveDataMode.Mask"/>.
    /// </summary>
    public SensitiveDataMode Mode { get; init; } = SensitiveDataMode.Mask;
}
