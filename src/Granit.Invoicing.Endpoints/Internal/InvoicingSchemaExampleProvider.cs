using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Invoicing.Endpoints.Dtos;

namespace Granit.Invoicing.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for Invoicing Request and Response DTOs.
/// </summary>
internal sealed class InvoicingSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(InvoiceCreateRequest)] = new JsonObject
            {
                ["documentType"] = "Invoice",
                ["currency"] = "EUR",
                ["collectionMethod"] = "ChargeAutomatically",
                ["billingReason"] = "Subscription",
                ["parentInvoiceId"] = null,
                ["creditNoteReason"] = null,
                ["periodStart"] = "2026-04-01T00:00:00+00:00",
                ["periodEnd"] = "2026-04-30T23:59:59+00:00",
            },
            [typeof(InvoiceResponse)] = new JsonObject
            {
                ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["documentType"] = "Invoice",
                ["invoiceNumber"] = "INV-2026-00042",
                ["status"] = "Open",
                ["collectionMethod"] = "ChargeAutomatically",
                ["billingReason"] = "Subscription",
                ["currency"] = "EUR",
                ["subtotal"] = 99.00m,
                ["taxTotal"] = 20.79m,
                ["total"] = 119.79m,
                ["amountPaid"] = 0.00m,
                ["amountCredited"] = 0.00m,
                ["amountRemaining"] = 119.79m,
                ["parentInvoiceId"] = null,
                ["creditNoteReason"] = null,
                ["issuedAt"] = "2026-04-20T10:00:00+00:00",
                ["dueAt"] = "2026-05-20T10:00:00+00:00",
                ["paidAt"] = null,
                ["periodStart"] = "2026-04-01T00:00:00+00:00",
                ["periodEnd"] = "2026-04-30T23:59:59+00:00",
                ["lineItems"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "01960f3a-5c9e-7c3b-b4a2-000000000001",
                        ["description"] = "Pro plan — April 2026",
                        ["quantity"] = 1m,
                        ["unitPrice"] = 99.00m,
                        ["amount"] = 99.00m,
                        ["taxRate"] = 0.21m,
                        ["taxAmount"] = 20.79m,
                        ["sourceType"] = "Subscription",
                        ["sourceId"] = "01960f3a-5c9e-7c3b-b4a2-abc111222333",
                        ["periodStart"] = "2026-04-01T00:00:00+00:00",
                        ["periodEnd"] = "2026-04-30T23:59:59+00:00",
                    },
                },
            },
        };
}
