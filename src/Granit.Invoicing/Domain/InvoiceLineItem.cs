using System.Diagnostics.CodeAnalysis;
using Granit.Domain;
using Granit.Invoicing.Domain.ValueObjects;

namespace Granit.Invoicing.Domain;

/// <summary>A line item on an invoice.</summary>
public sealed class InvoiceLineItem : Entity
{
    private InvoiceLineItem() { }

    /// <summary>Creates a new invoice line item.</summary>
    /// <param name="id">Unique line item identifier.</param>
    /// <param name="description">Human-readable line item description.</param>
    /// <param name="quantity">Quantity (must be positive).</param>
    /// <param name="unitPrice">Unit price before tax (non-negative).</param>
    /// <param name="source">Origin of this line item (subscription, metering, one-shot).</param>
    /// <param name="taxRate">Tax rate as a decimal fraction (e.g., 0.21 for 21%). Null if untaxed.</param>
    /// <param name="period">Billing period this line item covers.</param>
    /// <param name="productId">Optional <c>Granit.Catalog.Product</c> identifier — stable label across renames of the underlying meter or plan price.</param>
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Each parameter is a distinct domain concept (id, description, quantity, unit price, source, tax, period, product); a wrapper type would not represent any real aggregate.")]
    public static InvoiceLineItem Create(
        Guid id, string description, decimal quantity, decimal unitPrice,
        LineItemSource source,
        decimal? taxRate = null,
        BillingPeriod? period = null,
        Guid? productId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        ArgumentNullException.ThrowIfNull(source);

        // ADR-036: Usage / Subscription line items MUST carry a Guid SourceId pointing to
        // the originating MeterDefinition / Subscription (or PlanPrice) respectively.
        // OneShot / Credit lines remain free-form (e-commerce SKUs, manual adjustments).
        if (source.Type is InvoiceSourceType.Usage or InvoiceSourceType.Subscription)
        {
            if (string.IsNullOrWhiteSpace(source.Id))
            {
                throw new ArgumentException(
                    $"Invoice line items with SourceType '{source.Type}' must carry a non-empty SourceId " +
                    $"(see ADR-036).",
                    nameof(source));
            }

            if (!Guid.TryParse(source.Id, out _))
            {
                throw new ArgumentException(
                    $"Invoice line items with SourceType '{source.Type}' must carry a Guid SourceId. " +
                    $"Got '{source.Id}' (see ADR-036).",
                    nameof(source));
            }
        }

        decimal amount = quantity * unitPrice;
        decimal taxAmount = taxRate.HasValue ? amount * taxRate.Value : 0;

        return new InvoiceLineItem
        {
            Id = id,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = amount,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            SourceType = source.Type,
            SourceId = source.Id,
            PeriodStart = period?.Start,
            PeriodEnd = period?.End,
            ProductId = productId,
        };
    }

    /// <summary>Human-readable line item description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Quantity billed.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Unit price before tax.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Computed amount (Quantity * UnitPrice).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Tax rate as a decimal fraction (e.g., 0.21 for 21%). Null if untaxed.</summary>
    public decimal? TaxRate { get; private set; }

    /// <summary>Computed tax amount (Amount * TaxRate).</summary>
    public decimal TaxAmount { get; private set; }

    /// <summary>Origin type of this line item (subscription, metering, one-shot, etc.).</summary>
    public InvoiceSourceType SourceType { get; private set; }

    /// <summary>Optional source entity identifier for traceability.</summary>
    public string? SourceId { get; private set; }

    /// <summary>Billing period start covered by this line item.</summary>
    public DateTimeOffset? PeriodStart { get; private set; }

    /// <summary>Billing period end covered by this line item.</summary>
    public DateTimeOffset? PeriodEnd { get; private set; }

    /// <summary>
    /// Optional <c>Granit.Catalog.Product</c> identifier propagated from
    /// <c>MeterDefinition.ProductId</c> (Usage source) or <c>PlanPrice.ProductId</c> (Subscription source).
    /// Provides a stable, human-friendly label that survives renames of the underlying meter or plan price
    /// (audit-friendly). No SQL FK — cross-module reference; integrity is best-effort by convention.
    /// </summary>
    public Guid? ProductId { get; private set; }
}
