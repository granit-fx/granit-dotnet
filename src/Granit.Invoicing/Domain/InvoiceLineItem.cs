using Granit.Domain;

namespace Granit.Invoicing.Domain;

/// <summary>A line item on an invoice.</summary>
public sealed class InvoiceLineItem : Entity
{
    private InvoiceLineItem() { }

    /// <summary>Creates a new invoice line item.</summary>
    public static InvoiceLineItem Create(
        Guid id, string description, decimal quantity, decimal unitPrice,
        InvoiceSourceType sourceType, string? sourceId = null,
        decimal? taxRate = null,
        DateTimeOffset? periodStart = null, DateTimeOffset? periodEnd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
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
            SourceType = sourceType,
            SourceId = sourceId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
        };
    }

    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount { get; private set; }
    public decimal? TaxRate { get; private set; }
    public decimal TaxAmount { get; private set; }
    public InvoiceSourceType SourceType { get; private set; }
    public string? SourceId { get; private set; }
    public DateTimeOffset? PeriodStart { get; private set; }
    public DateTimeOffset? PeriodEnd { get; private set; }
}
