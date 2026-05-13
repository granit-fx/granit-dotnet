using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Granit.Browsing.Pages;

/// <summary>
/// Default <see cref="IHarScrubber"/> — redacts well-known auth headers, cookie headers
/// and entry cookie collections in a HAR 1.2 document. Body content is left intact (apps
/// override to mask request/response bodies when needed).
/// </summary>
public sealed class DefaultHarScrubber : IHarScrubber
{
    /// <summary>The replacement marker substituted for every redacted value.</summary>
    public const string RedactionMarker = "***";

    private static readonly HashSet<string> SensitiveRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
        "X-Api-Key",
        "X-Auth-Token",
    };

    private static readonly HashSet<string> SensitiveResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Set-Cookie",
        "Proxy-Authenticate",
    };

    /// <inheritdoc/>
    public string Scrub(string harJson)
    {
        ArgumentNullException.ThrowIfNull(harJson);
        if (harJson.Length == 0)
        {
            return harJson;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(harJson);
        }
        catch (JsonException)
        {
            // Surface the original document untouched — scrubbing a malformed HAR is the
            // caller's responsibility and we don't want to mask a parser error.
            return harJson;
        }

        if (root?["log"]?["entries"] is not JsonArray entries)
        {
            return harJson;
        }

        foreach (JsonNode? entryNode in entries)
        {
            if (entryNode is not JsonObject entry)
            {
                continue;
            }

            if (entry["request"] is JsonObject request)
            {
                ScrubHeaders(request["headers"] as JsonArray, SensitiveRequestHeaders);
                ScrubCookies(request["cookies"] as JsonArray);
            }
            if (entry["response"] is JsonObject response)
            {
                ScrubHeaders(response["headers"] as JsonArray, SensitiveResponseHeaders);
                ScrubCookies(response["cookies"] as JsonArray);
            }
        }

        return root.ToJsonString();
    }

    private static void ScrubHeaders(JsonArray? headers, HashSet<string> sensitive)
    {
        if (headers is null)
        {
            return;
        }

        foreach (JsonNode? node in headers)
        {
            if (node is not JsonObject header)
            {
                continue;
            }
            string? name = header["name"]?.GetValue<string>();
            if (name is not null && sensitive.Contains(name))
            {
                header["value"] = RedactionMarker;
            }
        }
    }

    private static void ScrubCookies(JsonArray? cookies)
    {
        if (cookies is null)
        {
            return;
        }

        foreach (JsonNode? node in cookies)
        {
            if (node is JsonObject cookie && cookie.ContainsKey("value"))
            {
                cookie["value"] = RedactionMarker;
            }
        }
    }
}
