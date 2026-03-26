namespace Granit.Mcp.Sanitization;

/// <summary>
/// Marks a property on a tool response DTO for redaction before the response
/// reaches the MCP client. Processed by <see cref="IMcpOutputSanitizer"/>
/// implementations in the SDK's <c>AddCallToolFilter</c> pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class McpRedactAttribute : Attribute
{
    /// <summary>
    /// The redaction strategy to apply. Default: <see cref="RedactionStrategy.Omit"/>.
    /// </summary>
    public RedactionStrategy Strategy { get; init; } = RedactionStrategy.Omit;
}
