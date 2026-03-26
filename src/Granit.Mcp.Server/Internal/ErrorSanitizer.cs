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

        // Detect sensitive data patterns and replace with generic message.
        if (ContainsSensitivePattern(text))
        {
            return "An internal error occurred. Check server logs for details.";
        }

        return text;
    }

    private static bool ContainsSensitivePattern(string text) =>
        // Connection strings
        text.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("User ID=", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
        // API keys and tokens
        text.Contains("Bearer ey", StringComparison.Ordinal) ||
        text.Contains("sk-", StringComparison.Ordinal) ||
        text.Contains("api_key=", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("apikey=", StringComparison.OrdinalIgnoreCase) ||
        // Internal paths
        text.Contains("/home/", StringComparison.Ordinal) ||
        text.Contains("C:\\Users\\", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("/var/", StringComparison.Ordinal) ||
        // Vault / secrets
        text.Contains("vault:secret/", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("VAULT_TOKEN", StringComparison.Ordinal) ||
        // Internal hostnames
        text.Contains(".internal", StringComparison.OrdinalIgnoreCase) ||
        text.Contains(".svc.cluster.local", StringComparison.OrdinalIgnoreCase);
}
