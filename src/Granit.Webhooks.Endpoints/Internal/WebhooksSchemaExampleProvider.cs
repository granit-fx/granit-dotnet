using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Webhooks.Endpoints.Dtos;

namespace Granit.Webhooks.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for Webhooks Request and Response DTOs.
/// </summary>
internal sealed class WebhooksSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(WebhookSubscriptionCreateRequest)] = new JsonObject
            {
                ["targetUrl"] = "https://hooks.acme.example/granit/orders",
                ["eventType"] = "order.fulfilled",
            },
            [typeof(WebhookSubscriptionUpdateRequest)] = new JsonObject
            {
                ["targetUrl"] = "https://hooks.acme.example/granit/orders-v2",
            },
            [typeof(WebhookSubscriptionDeactivateRequest)] = new JsonObject
            {
                ["reason"] = "Migrated to the new order pipeline.",
            },
            [typeof(WebhookSubscriptionResponse)] = new JsonObject
            {
                ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["targetUrl"] = "https://hooks.acme.example/granit/orders",
                ["eventType"] = "order.fulfilled",
                ["status"] = "Active",
                ["consecutiveFailureCount"] = 0,
                ["lastSuccessAt"] = "2026-04-20T13:45:12+00:00",
                ["createdAt"] = "2026-03-15T09:00:00+00:00",
                ["modifiedAt"] = null,
                ["signingSecretHint"] = "whsec_b46a****************5182",
            },
        };
}
