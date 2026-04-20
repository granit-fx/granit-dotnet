using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Subscriptions.Endpoints.Dtos;

namespace Granit.Subscriptions.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for Subscriptions Request and Response DTOs.
/// </summary>
internal sealed class SubscriptionsSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            // ──── Plans ────
            [typeof(PlanCreateRequest)] = new JsonObject
            {
                ["name"] = "Pro",
                ["description"] = "For growing teams — unlimited projects and priority support.",
                ["pricingModel"] = "Flat",
                ["defaultInterval"] = "Monthly",
                ["trialDays"] = 14,
                ["seatLimit"] = 25,
            },
            [typeof(PlanUpdateRequest)] = new JsonObject
            {
                ["name"] = "Pro",
                ["description"] = "For growing teams — unlimited projects, priority support, SSO.",
                ["sortOrder"] = 20,
            },
            [typeof(PlanResponse)] = new JsonObject
            {
                ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["name"] = "Pro",
                ["description"] = "For growing teams — unlimited projects and priority support.",
                ["pricingModel"] = "Flat",
                ["defaultInterval"] = "Monthly",
                ["trialDays"] = 14,
                ["seatLimit"] = 25,
                ["sortOrder"] = 20,
                ["lifecycleStatus"] = "Active",
                ["prices"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc111222333",
                        ["amount"] = 99.00m,
                        ["currency"] = "EUR",
                        ["interval"] = "Monthly",
                        ["effectiveFrom"] = "2026-01-01T00:00:00+00:00",
                        ["isCurrent"] = true,
                        ["replacedByPriceId"] = null,
                        ["replacedAt"] = null,
                    },
                },
            },

            // ──── Subscriptions ────
            [typeof(SubscriptionCreateRequest)] = new JsonObject
            {
                ["planId"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["currency"] = "EUR",
                ["trialEndsAt"] = "2026-05-04T00:00:00+00:00",
            },
            [typeof(SubscriptionCancelRequest)] = new JsonObject
            {
                ["reason"] = "Customer switching to annual billing.",
                ["atPeriodEnd"] = true,
            },
            [typeof(SubscriptionChangePlanRequest)] = new JsonObject
            {
                ["newPlanId"] = "01960f3a-5c9e-7c3b-b4a2-def456abc789",
            },
        };
}
