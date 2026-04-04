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
    public static InvoiceLineItem Create(
        Guid id, string description, decimal quantity, decimal unitPrice,
        LineItemSource source,
        decimal? taxRate = null,
        BillingPeriod? period = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        ArgumentNullException.ThrowIfNull(source);
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
}
