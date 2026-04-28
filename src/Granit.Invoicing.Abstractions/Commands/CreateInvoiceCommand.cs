using Granit.Invoicing.Domain;

namespace Granit.Invoicing.Commands;

/// <summary>
/// Command to create an invoice. Can be published by any module
/// (Subscriptions, Commerce, admin) without creating a dependency on Invoicing.
/// </summary>
/// <param name="TenantId">The tenant for which to create the invoice (multi-tenant isolation).</param>
/// <param name="Currency">ISO 4217 currency code (e.g., "EUR").</param>
/// <param name="CollectionMethod">How payment should be collected.</param>
/// <param name="BillingReason">Why the invoice is being created.</param>
/// <param name="LineItems">Line items to add to the invoice.</param>
/// <param name="PartyId">
///   Optional identifier of the <c>Granit.Parties.Party</c> that holds the billing identity for this invoice.
///   When <c>null</c>, the invoicing service resolves the tenant's default host-scoped party via
///   <c>IDefaultPartyResolver.GetDefaultForTenantAsync</c>. Pass an explicit value when the caller already
///   knows the party (admin endpoints, manually issued invoices, multi-party tenants).
/// </param>
/// <param name="PeriodStart">Optional billing period start.</param>
/// <param name="PeriodEnd">Optional billing period end.</param>
/// <param name="IdempotencyKey">Optional key to prevent duplicate invoice creation.</param>
public sealed record CreateInvoiceCommand(
    Guid TenantId,
    string Currency,
    CollectionMethod CollectionMethod,
    BillingReason BillingReason,
    IReadOnlyList<CreateInvoiceLineItem> LineItems,
    Guid? PartyId = null,
    DateTimeOffset? PeriodStart = null,
    DateTimeOffset? PeriodEnd = null,
    string? IdempotencyKey = null);

/// <summary>Line item data for invoice creation.</summary>
/// <param name="Description">Display description.</param>
/// <param name="Quantity">Quantity.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="SourceType">Origin (Subscription, Usage, OneShot, Credit).</param>
/// <param name="SourceId">
///   Source entity identifier. Convention (see ADR-036): when <see cref="SourceType"/> is
///   <see cref="InvoiceSourceType.Usage"/>, this MUST be the <c>MeterDefinition.Id</c> as a Guid string;
///   when <see cref="SourceType"/> is <see cref="InvoiceSourceType.Subscription"/>, this MUST be the
///   <c>Subscription.Id</c> (or <c>PlanPrice.Id</c>) as a Guid string. <see cref="InvoiceSourceType.OneShot"/>
///   and <see cref="InvoiceSourceType.Credit"/> are free-form.
/// </param>
/// <param name="ProductId">
///   Optional <c>Granit.Catalog.Product</c> identifier. Should be propagated from the upstream entity's
///   <c>ProductId</c> (e.g. <c>MeterDefinition.ProductId</c>, <c>PlanPrice.ProductId</c>) so that the
///   invoice line carries a stable label across renames of the underlying source.
/// </param>
public sealed record CreateInvoiceLineItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    InvoiceSourceType SourceType,
    string? SourceId = null,
    Guid? ProductId = null);
