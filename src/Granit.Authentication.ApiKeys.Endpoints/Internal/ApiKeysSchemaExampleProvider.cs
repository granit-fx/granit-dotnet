using System.Text.Json.Nodes;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Http.ApiDocumentation;

namespace Granit.Authentication.ApiKeys.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for API key Request DTOs.
/// </summary>
internal sealed class ApiKeysSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(ApiKeyCreateRequest)] = new JsonObject
            {
                ["name"] = "CI/CD Pipeline",
                ["type"] = "Secret",
                ["environment"] = "live",
                ["permissions"] = new JsonArray { "BlobStorage.Read", "DataExchange.Export" },
                ["allowedCidrs"] = new JsonArray { "10.0.0.0/8" },
                ["expiresAt"] = "2027-01-01T00:00:00+00:00",
                ["cacheBehavior"] = "Normal",
            },
            [typeof(ApiKeyUpdateScopesRequest)] = new JsonObject
            {
                ["permissions"] = new JsonArray { "BlobStorage.Read", "BlobStorage.Write" },
                ["allowedCidrs"] = new JsonArray { "10.0.0.0/8", "172.16.0.0/12" },
            },
        };
}
