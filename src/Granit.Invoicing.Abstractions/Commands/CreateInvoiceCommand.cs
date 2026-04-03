using Granit.Invoicing.Domain;

namespace Granit.Invoicing.Commands;

/// <summary>
/// Command to create an invoice. Can be published by any module
/// (Subscriptions, Commerce, admin) without creating a dependency on Invoicing.
/// </summary>
/// <param name="TenantId">The tenant for which to create the invoice.</param>
/// <param name="Currency">ISO 4217 currency code (e.g., "EUR").</param>
/// <param name="CollectionMethod">How payment should be collected.</param>
/// <param name="BillingReason">Why the invoice is being created.</param>
/// <param name="LineItems">Line items to add to the invoice.</param>
/// <param name="PeriodStart">Optional billing period start.</param>
/// <param name="PeriodEnd">Optional billing period end.</param>
/// <param name="IdempotencyKey">Optional key to prevent duplicate invoice creation.</param>
public sealed record CreateInvoiceCommand(
    Guid TenantId,
    string Currency,
    CollectionMethod CollectionMethod,
    BillingReason BillingReason,
    IReadOnlyList<CreateInvoiceLineItem> LineItems,
    DateTimeOffset? PeriodStart = null,
    DateTimeOffset? PeriodEnd = null,
    string? IdempotencyKey = null);

/// <summary>Line item data for invoice creation.</summary>
/// <param name="Description">Display description.</param>
/// <param name="Quantity">Quantity.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="SourceType">Origin (Subscription, Usage, OneShot, Credit).</param>
/// <param name="SourceId">Optional source entity identifier.</param>
public sealed record CreateInvoiceLineItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    InvoiceSourceType SourceType,
    string? SourceId = null);
