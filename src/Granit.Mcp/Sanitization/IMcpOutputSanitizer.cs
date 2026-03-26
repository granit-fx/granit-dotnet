using ModelContextProtocol.Protocol;

namespace Granit.Mcp.Sanitization;

/// <summary>
/// Sanitizes MCP tool call results before they reach the client.
/// Implementations are invoked via the SDK's <c>AddCallToolFilter</c> pipeline.
/// </summary>
/// <remarks>
/// Multiple sanitizers compose in registration order. Each receives the result
/// from the previous sanitizer.
/// </remarks>
public interface IMcpOutputSanitizer
{
    /// <summary>
    /// Sanitizes a tool call result.
    /// </summary>
    /// <param name="result">The tool call result to sanitize.</param>
    /// <param name="services">Request-scoped service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sanitized result.</returns>
    ValueTask<CallToolResult> SanitizeAsync(
        CallToolResult result,
        IServiceProvider services,
        CancellationToken cancellationToken = default);
}
