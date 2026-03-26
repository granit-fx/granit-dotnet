using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Granit.Mcp.Sanitization;

/// <summary>
/// Processes <see cref="McpRedactAttribute"/> annotations on MCP tool response DTOs.
/// Applies the configured <see cref="RedactionStrategy"/> (Omit, Hash, or Mask) to
/// sensitive properties before the response reaches the MCP client.
/// </summary>
internal sealed class PropertyRedactionSanitizer : IMcpOutputSanitizer
{
    public ValueTask<CallToolResult> SanitizeAsync(
        CallToolResult result,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (result.Content is not { Count: > 0 })
        {
            return ValueTask.FromResult(result);
        }

        List<ContentBlock> sanitizedContent = [];
        bool modified = false;

        foreach (ContentBlock content in result.Content)
        {
            if (content is TextContentBlock textBlock && IsJsonLike(textBlock.Text))
            {
                string sanitized = RedactJsonProperties(textBlock.Text);
                if (!ReferenceEquals(sanitized, textBlock.Text))
                {
                    sanitizedContent.Add(new TextContentBlock
                    {
                        Text = sanitized,
                        Annotations = textBlock.Annotations,
                    });
                    modified = true;
                    continue;
                }
            }

            sanitizedContent.Add(content);
        }

        return modified
            ? ValueTask.FromResult(new CallToolResult
            {
                Content = sanitizedContent,
                IsError = result.IsError,
            })
            : ValueTask.FromResult(result);
    }

    private static bool IsJsonLike(string text) =>
        text.Length > 0 && text[0] is '{' or '[';

    /// <summary>
    /// Scans JSON content for property names matching known <see cref="McpRedactAttribute"/>
    /// patterns and applies the configured redaction strategy.
    /// </summary>
    private static string RedactJsonProperties(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
            {
                return json;
            }

            bool changed = RedactNode(node);
            return changed ? node.ToJsonString() : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static bool RedactNode(JsonNode node)
    {
        bool changed = false;

        if (node is JsonObject obj)
        {
            List<string> keysToRemove = [];
            List<(string Key, string Value)> keysToReplace = [];

            foreach ((string key, JsonNode? value) in obj)
            {
                if (IsSensitivePropertyName(key))
                {
                    RedactionStrategy strategy = GetStrategy(key);
                    switch (strategy)
                    {
                        case RedactionStrategy.Omit:
                            keysToRemove.Add(key);
                            changed = true;
                            break;
                        case RedactionStrategy.Hash:
                            if (value is not null)
                            {
                                keysToReplace.Add((key, HashValue(value.ToString())));
                                changed = true;
                            }
                            break;
                        case RedactionStrategy.Mask:
                            if (value is not null)
                            {
                                keysToReplace.Add((key, MaskValue(value.ToString())));
                                changed = true;
                            }
                            break;
                    }
                }
                else if (value is not null)
                {
                    changed |= RedactNode(value);
                }
            }

            foreach (string key in keysToRemove)
            {
                obj.Remove(key);
            }

            foreach ((string key, string replacement) in keysToReplace)
            {
                obj[key] = replacement;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (JsonNode? item in arr)
            {
                if (item is not null)
                {
                    changed |= RedactNode(item);
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Well-known sensitive property names that should be redacted by default.
    /// Matches regardless of whether a <see cref="McpRedactAttribute"/> is declared.
    /// </summary>
    private static bool IsSensitivePropertyName(string name) =>
        name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("apiKey", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("connectionString", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("connection_string", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ssn", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("socialSecurity", StringComparison.OrdinalIgnoreCase);

    private static RedactionStrategy GetStrategy(string name)
    {
        // Tokens and IDs use Hash for correlation; everything else is Omit.
        if (name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("id", StringComparison.OrdinalIgnoreCase))
        {
            return RedactionStrategy.Hash;
        }

        return RedactionStrategy.Omit;
    }

    internal static string HashValue(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexStringLower(hash)[..16]}";
    }

    internal static string MaskValue(string value)
    {
        if (value.Length <= 4)
        {
            return "****";
        }

        return $"{value[..2]}{"".PadRight(value.Length - 4, '*')}{value[^2..]}";
    }
}
