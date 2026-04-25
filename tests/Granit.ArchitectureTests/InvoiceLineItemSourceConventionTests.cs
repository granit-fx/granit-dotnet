using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// ADR-036 — Invoicing line item source convention.
///
/// <para>
/// Lines whose <see cref="InvoiceSourceType"/> is <see cref="InvoiceSourceType.Usage"/> or
/// <see cref="InvoiceSourceType.Subscription"/> MUST carry a Guid <c>SourceId</c>. The Guid
/// references the originating <c>MeterDefinition.Id</c> (Usage) or
/// <c>Subscription.Id</c> / <c>PlanPrice.Id</c> (Subscription) — making invoices traceable
/// back to the entity that produced the charge for audit (ISO 27001 A.12.4) and reconciliation.
/// </para>
///
/// <para>
/// <see cref="InvoiceSourceType.OneShot"/> and <see cref="InvoiceSourceType.Credit"/> remain
/// free-form: they cover e-commerce SKUs, manual adjustments, and refunds where the source
/// identifier may be a string code rather than a Guid (or absent altogether).
/// </para>
///
/// <para>
/// The invariant is enforced at the entity boundary in <see cref="InvoiceLineItem.Create"/>;
/// these tests document the contract and guard against accidental relaxation.
/// </para>
/// </summary>
public sealed class InvoiceLineItemSourceConventionTests
{
    [Theory]
    [InlineData(InvoiceSourceType.Usage)]
    [InlineData(InvoiceSourceType.Subscription)]
    public void Usage_and_Subscription_lines_MUST_carry_a_SourceId(InvoiceSourceType sourceType)
    {
        Should.Throw<ArgumentException>(() => InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(sourceType)));
    }

    [Theory]
    [InlineData(InvoiceSourceType.Usage)]
    [InlineData(InvoiceSourceType.Subscription)]
    public void Usage_and_Subscription_lines_MUST_carry_a_Guid_SourceId(InvoiceSourceType sourceType)
    {
        Should.Throw<ArgumentException>(() => InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(sourceType, "not-a-guid")));
    }

    [Theory]
    [InlineData(InvoiceSourceType.Usage)]
    [InlineData(InvoiceSourceType.Subscription)]
    public void Usage_and_Subscription_lines_accept_a_Guid_SourceId(InvoiceSourceType sourceType)
    {
        var sourceId = Guid.NewGuid();

        var lineItem = InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(sourceType, sourceId.ToString()));

        lineItem.SourceType.ShouldBe(sourceType);
        lineItem.SourceId.ShouldBe(sourceId.ToString());
    }

    [Theory]
    [InlineData(InvoiceSourceType.OneShot)]
    [InlineData(InvoiceSourceType.Credit)]
    public void OneShot_and_Credit_lines_remain_free_form(InvoiceSourceType sourceType)
    {
        var lineItemNullId = InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(sourceType));

        var lineItemFreeForm = InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(sourceType, "SKU-123"));

        lineItemNullId.SourceId.ShouldBeNull();
        lineItemFreeForm.SourceId.ShouldBe("SKU-123");
    }

    [Fact]
    public void ProductId_is_optional_and_propagates_when_provided()
    {
        var productId = Guid.NewGuid();

        var withProduct = InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(InvoiceSourceType.Usage, Guid.NewGuid().ToString()),
            productId: productId);

        var withoutProduct = InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test",
            quantity: 1m,
            unitPrice: 10m,
            source: new LineItemSource(InvoiceSourceType.OneShot, "SKU-9"));

        withProduct.ProductId.ShouldBe(productId);
        withoutProduct.ProductId.ShouldBeNull();
    }
}
