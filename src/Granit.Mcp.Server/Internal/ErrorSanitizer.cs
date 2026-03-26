using Granit.Mcp.Sanitization;
using ModelContextProtocol.Protocol;

namespace Granit.Mcp.Server.Internal;

/// <summary>
/// Strips stack traces and connection strings from error responses
/// to prevent information leakage to MCP clients.
/// </summary>
internal sealed class ErrorSanitizer : IMcpOutputSanitizer
{
    public ValueTask<CallToolResult> SanitizeAsync(
        CallToolResult result,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (result.IsError != true)
        {
            return ValueTask.FromResult(result);
        }

        List<ContentBlock> sanitizedContent = [];
        foreach (ContentBlock content in result.Content)
        {
            if (content is TextContentBlock textBlock)
            {
                sanitizedContent.Add(new TextContentBlock
                {
                    Text = SanitizeErrorText(textBlock.Text),
                    Annotations = textBlock.Annotations,
                });
            }
            else
            {
                sanitizedContent.Add(content);
            }
        }

        return ValueTask.FromResult(new CallToolResult
        {
            Content = sanitizedContent,
            IsError = true,
        });
    }

    private static string SanitizeErrorText(string text)
    {
        // Remove stack traces (lines starting with "   at ")
        int stackTraceIndex = text.IndexOf("\n   at ", StringComparison.Ordinal);
        if (stackTraceIndex >= 0)
        {
            text = text[..stackTraceIndex];
        }

        // Remove connection strings (common patterns)
        if (text.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return "An internal error occurred. Check server logs for details.";
        }

        return text;
    }
}
