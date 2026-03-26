namespace Granit.DataProtection;

/// <summary>
/// Classification level for sensitive data (ISO 27001 A.8.2 — Information classification).
/// </summary>
/// <remarks>
/// <para>
/// Consumers use the level to apply context-dependent protection:
/// </para>
/// <list type="bullet">
///   <item><b>MCP (AI agent)</b>: redacts <see cref="Confidential"/> and above.</item>
///   <item><b>MCP (admin tool)</b>: redacts <see cref="Restricted"/> only.</item>
///   <item><b>Audit trail</b>: applies <see cref="SensitiveDataMode"/> for all levels.</item>
///   <item><b>Logging</b>: redacts <see cref="Confidential"/> and above.</item>
///   <item><b>Data export (GDPR)</b>: flags all levels for special handling.</item>
/// </list>
/// </remarks>
public enum Sensitivity
{
    /// <summary>
    /// Low-sensitivity personal data — not a direct identifier on its own.
    /// Examples: first name, last name, username, display name, job title.
    /// </summary>
    Internal = 0,

    /// <summary>
    /// PII that can identify a person directly or indirectly.
    /// Examples: email, phone number, IP address, postal address, date of birth.
    /// </summary>
    Confidential = 1,

    /// <summary>
    /// Highly sensitive data — secrets, credentials, or special-category PII (GDPR Art. 9).
    /// Examples: password hash, API key, token, SSN, health data, bank account, biometric data.
    /// </summary>
    Restricted = 2,
}
