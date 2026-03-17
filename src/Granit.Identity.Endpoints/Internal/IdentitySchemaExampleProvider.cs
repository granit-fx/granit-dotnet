using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Identity.Endpoints.Dtos;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for identity Request DTOs.
/// </summary>
internal sealed class IdentitySchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(IdentityUserCacheBatchRequest)] = new JsonObject
            {
                ["userIds"] = new JsonArray
                {
                    "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                    "b2c3d4e5-f6a7-8901-bcde-f12345678901",
                },
            },
            [typeof(IdentityUserCacheSyncRequest)] = new JsonObject
            {
                ["userIds"] = new JsonArray
                {
                    "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                },
            },
            [typeof(IdentityWebhookPayload)] = new JsonObject
            {
                ["eventType"] = "user_updated",
                ["userId"] = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                ["timestamp"] = "2026-03-06T14:30:00+01:00",
            },
        };
}
