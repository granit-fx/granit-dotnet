namespace Granit.Mcp.Sanitization;

/// <summary>
/// Strategy for redacting sensitive properties in MCP tool responses.
/// </summary>
public enum RedactionStrategy
{
    /// <summary>
    /// Remove the property entirely from the output. Default and recommended
    /// for LLM-consumed output — masked values waste tokens and can trigger
    /// hallucinations.
    /// </summary>
    Omit,

    /// <summary>
    /// Replace the value with a stable SHA-256 hash. Useful for correlation
    /// across tool calls without exposing the actual value.
    /// </summary>
    Hash,

    /// <summary>
    /// Replace the value with a masked representation (e.g., <c>j***@***.com</c>).
    /// Reserved for UI-facing scenarios, not recommended for LLM output.
    /// </summary>
    Mask,
}
