using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies.Endpoints.Dtos;

namespace Granit.Http.Cookies.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for cookie consent Response DTOs.
/// </summary>
internal sealed class CookiesSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(CookieConsentConfigResponse)] = new JsonObject
            {
                ["cookies"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = ".Granit.Session",
                        ["category"] = "strictly_necessary",
                        ["retentionDays"] = 1,
                        ["purpose"] = "Maintains the authenticated session.",
                    },
                    new JsonObject
                    {
                        ["name"] = ".Granit.Locale",
                        ["category"] = "preferences",
                        ["retentionDays"] = 365,
                        ["purpose"] = "Stores the user's preferred language.",
                    },
                },
                ["services"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "matomo",
                        ["category"] = "analytics",
                        ["cookiePatterns"] = new JsonArray { "^_pk_", "^mtm_" },
                    },
                },
            },
        };
}
