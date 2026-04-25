using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Tests.Domain;

public sealed class InvoiceLineItemTests
{
    // ======== Create ========

    [Fact]
    public void Create_ShouldCalculateAmount()
    {
        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Widget", 3, 10.00m,
            new LineItemSource(InvoiceSourceType.OneShot));

        lineItem.Amount.ShouldBe(30.00m);
        lineItem.Quantity.ShouldBe(3m);
        lineItem.UnitPrice.ShouldBe(10.00m);
    }

    [Fact]
    public void Create_WithTaxRate_ShouldCalculateTaxAmount()
    {
        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Pro Plan", 1, 100.00m,
            new LineItemSource(InvoiceSourceType.Subscription, Guid.NewGuid().ToString()),
            taxRate: 0.21m);

        lineItem.TaxRate.ShouldBe(0.21m);
        lineItem.TaxAmount.ShouldBe(21.00m);
        lineItem.Amount.ShouldBe(100.00m);
    }

    [Fact]
    public void Create_WithoutTaxRate_ShouldSetZeroTaxAmount()
    {
        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Consulting", 2, 50.00m,
            new LineItemSource(InvoiceSourceType.OneShot));

        lineItem.TaxRate.ShouldBeNull();
        lineItem.TaxAmount.ShouldBe(0m);
    }

    [Fact]
    public void Create_WithBillingPeriod_ShouldSetPeriodDates()
    {
        DateTimeOffset start = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Monthly sub", 1, 29.99m,
            new LineItemSource(InvoiceSourceType.Subscription, Guid.NewGuid().ToString()),
            period: new BillingPeriod(start, end));

        lineItem.PeriodStart.ShouldBe(start);
        lineItem.PeriodEnd.ShouldBe(end);
    }

    [Fact]
    public void Create_ShouldSetSourceFields()
    {
        var meterId = Guid.NewGuid();
        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Usage charges", 150, 0.05m,
            new LineItemSource(InvoiceSourceType.Usage, meterId.ToString()));

        lineItem.SourceType.ShouldBe(InvoiceSourceType.Usage);
        lineItem.SourceId.ShouldBe(meterId.ToString());
    }

    [Fact]
    public void Create_WithSourceTypeOnly_ShouldSetSourceIdNull()
    {
        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Credit adjustment", 1, 10.00m,
            new LineItemSource(InvoiceSourceType.Credit));

        lineItem.SourceType.ShouldBe(InvoiceSourceType.Credit);
        lineItem.SourceId.ShouldBeNull();
    }

    // ======== Validation ========

    [Fact]
    public void Create_WithEmptyDescription_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            InvoiceLineItem.Create(
                Guid.NewGuid(), "", 1, 10.00m,
                new LineItemSource(InvoiceSourceType.OneShot)));
    }

    [Fact]
    public void Create_WithZeroQuantity_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            InvoiceLineItem.Create(
                Guid.NewGuid(), "Item", 0, 10.00m,
                new LineItemSource(InvoiceSourceType.OneShot)));
    }

    [Fact]
    public void Create_WithNegativeUnitPrice_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            InvoiceLineItem.Create(
                Guid.NewGuid(), "Item", 1, -5.00m,
                new LineItemSource(InvoiceSourceType.OneShot)));
    }

    [Fact]
    public void Create_WithNullSource_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() =>
            InvoiceLineItem.Create(
                Guid.NewGuid(), "Item", 1, 10.00m, null!));
    }

    [Fact]
    public void Create_WithZeroUnitPrice_ShouldSucceed()
    {
        var lineItem = InvoiceLineItem.Create(
            Guid.NewGuid(), "Free trial", 1, 0m,
            new LineItemSource(InvoiceSourceType.Subscription, Guid.NewGuid().ToString()));

        lineItem.Amount.ShouldBe(0m);
        lineItem.UnitPrice.ShouldBe(0m);
    }
}
