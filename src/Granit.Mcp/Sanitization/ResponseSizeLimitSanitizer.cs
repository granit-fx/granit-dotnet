using System.Text;
using System.Text.Json;
using Granit.Mcp.Options;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Granit.Mcp.Sanitization;

/// <summary>
/// Truncates tool call results that exceed the configured maximum response size.
/// </summary>
internal sealed class ResponseSizeLimitSanitizer(IOptions<GranitMcpOptions> options) : IMcpOutputSanitizer
{
    public ValueTask<CallToolResult> SanitizeAsync(
        CallToolResult result,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        int maxBytes = options.Value.MaxResponseSizeBytes;
        if (maxBytes <= 0)
        {
            return ValueTask.FromResult(result);
        }

        string json = JsonSerializer.Serialize(result.Content);
        int byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount <= maxBytes)
        {
            return ValueTask.FromResult(result);
        }

        return ValueTask.FromResult(new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = $"[Response truncated: {byteCount:N0} bytes exceeded {maxBytes:N0} byte limit]",
            }],
        });
    }
}
