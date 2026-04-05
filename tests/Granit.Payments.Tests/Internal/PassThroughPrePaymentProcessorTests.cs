using Granit.Invoicing;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.Payments.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Internal;

public sealed class PassThroughPrePaymentProcessorTests
{
    private readonly PassThroughPrePaymentProcessor _sut = new();

    // ======== Happy Path ========

    [Fact]
    public async Task ProcessAsync_ShouldReturnFullInvoiceTotal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var eto = new InvoiceFinalizedEto(Guid.NewGuid(), Guid.NewGuid(), 250.75m, "EUR", CollectionMethod.Auto);

        PrePaymentResult result = await _sut.ProcessAsync(eto, ct);

        result.RemainingAmount.ShouldBe(250.75m);
    }

    [Fact]
    public async Task ProcessAsync_ZeroTotal_ShouldReturnZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var eto = new InvoiceFinalizedEto(Guid.NewGuid(), Guid.NewGuid(), 0m, "USD", CollectionMethod.Auto);

        PrePaymentResult result = await _sut.ProcessAsync(eto, ct);

        result.RemainingAmount.ShouldBe(0m);
    }
}
