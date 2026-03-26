using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.DataProtection;
using ModelContextProtocol.Protocol;

namespace Granit.Mcp.Sanitization;

/// <summary>
/// Redacts sensitive properties from MCP tool JSON responses before they reach the client.
/// </summary>
/// <remarks>
/// <para>
/// Combines two redaction sources:
/// <list type="bullet">
///   <item><b><see cref="SensitivePropertyRegistry"/></b>: properties marked with
///   <see cref="SensitiveDataAttribute"/> on entity/DTO types across the framework.
///   The <see cref="SensitiveDataMode"/> determines the strategy (Mask, Omit, Hash).</item>
///   <item><b>Well-known names</b>: hardcoded fallback for property names that always
///   indicate secrets (password, connectionString, apiKey, etc.).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class PropertyRedactionSanitizer(SensitivePropertyRegistry registry) : IMcpOutputSanitizer
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

    private string RedactJsonProperties(string json)
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

    private bool RedactNode(JsonNode node)
    {
        bool changed = false;

        if (node is JsonObject obj)
        {
            List<string> keysToRemove = [];
            List<(string Key, string Value)> keysToReplace = [];

            foreach ((string key, JsonNode? value) in obj)
            {
                SensitiveDataMode? mode = ResolveSensitiveMode(key);
                if (mode is not null)
                {
                    switch (mode.Value)
                    {
                        case SensitiveDataMode.Omit:
                            keysToRemove.Add(key);
                            changed = true;
                            break;
                        case SensitiveDataMode.Hash:
                            if (value is not null)
                            {
                                keysToReplace.Add((key, HashValue(value.ToString())));
                                changed = true;
                            }
                            break;
                        case SensitiveDataMode.Mask:
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
            foreach (JsonNode item in arr.Where(item => item is not null)!)
            {
                changed |= RedactNode(item);
            }
        }

        return changed;
    }

    /// <summary>
    /// Resolves the sensitivity mode for a JSON property name by checking the
    /// <see cref="SensitivePropertyRegistry"/> first, then falling back to
    /// well-known secret property names. MCP applies redaction for
    /// <see cref="Sensitivity.Confidential"/> and above by default.
    /// </summary>
    private SensitiveDataMode? ResolveSensitiveMode(string propertyName)
    {
        // 1. Registry: [SensitiveData] annotations — redact Confidential+ for MCP
        if (registry.IsSensitiveAtLevel(propertyName, Sensitivity.Confidential, out SensitivePropertyEntry entry))
        {
            return entry.Mode;
        }

        // 2. Well-known secret names — always Omit (defense in depth)
        if (IsWellKnownSecretName(propertyName))
        {
            return SensitiveDataMode.Omit;
        }

        return null;
    }

    /// <summary>
    /// Hardcoded fallback for property names that always indicate secrets,
    /// regardless of whether a <see cref="SensitiveDataAttribute"/> is declared.
    /// </summary>
    private static bool IsWellKnownSecretName(string name) =>
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
