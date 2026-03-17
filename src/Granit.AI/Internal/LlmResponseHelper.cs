using System.Text.Json;

namespace Granit.AI.Internal;

/// <summary>
/// Shared utilities for parsing LLM responses across AI modules.
/// </summary>
internal static class LlmResponseHelper
{
    /// <summary>
    /// Strips optional Markdown code fences (```json ... ```) from an LLM response.
    /// </summary>
    public static string StripMarkdownCodeFences(string text)
    {
        ReadOnlySpan<char> span = text.AsSpan().Trim();

        if (span.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            span = span["```json".Length..];
        }
        else if (span.StartsWith("```", StringComparison.Ordinal))
        {
            span = span["```".Length..];
        }

        if (span.EndsWith("```", StringComparison.Ordinal))
        {
            span = span[..^"```".Length];
        }

        return span.Trim().ToString();
    }
}
