using Granit.Invoicing.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Tests.Domain.ValueObjects;

public sealed class InvoiceIdTests
{
    // ======== Create ========

    [Fact]
    public void Create_ShouldWrapGuid()
    {
        var raw = Guid.NewGuid();

        var invoiceId = InvoiceId.Create(raw);

        invoiceId.Value.ShouldBe(raw);
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => InvoiceId.Create(Guid.Empty));

    // ======== Implicit conversions ========

    [Fact]
    public void ImplicitConversionToGuid_ShouldReturnValue()
    {
        var raw = Guid.NewGuid();
        var invoiceId = InvoiceId.Create(raw);

        Guid converted = invoiceId;

        converted.ShouldBe(raw);
    }

    [Fact]
    public void ImplicitConversionFromGuid_ShouldCreateInvoiceId()
    {
        var raw = Guid.NewGuid();

        InvoiceId invoiceId = raw;

        invoiceId.Value.ShouldBe(raw);
    }

    [Fact]
    public void ImplicitConversionFromEmptyGuid_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
        {
            InvoiceId _ = Guid.Empty;
        });
    }
}
