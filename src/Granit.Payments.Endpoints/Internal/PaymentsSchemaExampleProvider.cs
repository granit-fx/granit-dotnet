using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Payments.Endpoints.Dtos;

namespace Granit.Payments.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for Payments Response DTOs
/// (payment methods, transactions, refunds, disputes, checkout sessions).
/// </summary>
internal sealed class PaymentsSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(PaymentMethodResponse)] = new JsonObject
            {
                ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["type"] = "card",
                ["providerName"] = "Stripe",
                ["providerMethodId"] = "pm_1PabcDEF1234567890",
                ["displayLabel"] = "Visa ending in 4242",
                ["isDefault"] = true,
                ["expiresAt"] = "2029-12-31T23:59:59+00:00",
                ["tenantId"] = "01960f3a-5c9e-7c3b-b4a2-def456abc789",
            },
            [typeof(PaymentAvailableMethodResponse)] = new JsonObject
            {
                ["methodType"] = "card",
                ["category"] = "Card",
                ["providerName"] = "Stripe",
                ["displayLabel"] = "Credit or debit card",
                ["capability"] = null,
            },
            [typeof(PaymentTransactionResponse)] = new JsonObject
            {
                ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc111222333",
                ["invoiceId"] = "01960f3a-5c9e-7c3b-b4a2-def000000001",
                ["amount"] = 119.79m,
                ["currency"] = "EUR",
                ["status"] = "Succeeded",
                ["providerName"] = "Stripe",
                ["providerTransactionId"] = "pi_3PabcDEF1234567890",
                ["paymentMethodId"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["actionUrl"] = null,
                ["idempotencyKey"] = "invoice-INV-2026-00042-attempt-1",
                ["failureCode"] = null,
                ["succeededAt"] = "2026-04-20T10:05:12+00:00",
                ["canceledAt"] = null,
                ["refunds"] = new JsonArray(),
                ["disputes"] = new JsonArray(),
                ["tenantId"] = "01960f3a-5c9e-7c3b-b4a2-def456abc789",
            },
            [typeof(PaymentCheckoutSessionResponse)] = new JsonObject
            {
                ["url"] = "https://checkout.stripe.com/c/pay/cs_test_a1b2c3d4...",
                ["sessionId"] = "cs_test_a1b2c3d4e5f67890",
                ["expiresAt"] = "2026-04-20T11:05:12+00:00",
            },
        };
}
