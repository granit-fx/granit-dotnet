using Granit.Commands;
using Granit.Invoicing;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Payments.Domain;
using Granit.Payments.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Internal;

public sealed class DefaultAutoChargeServiceTests
{
    // ======== Fixtures ========

    private readonly IInvoicePrePaymentProcessor _prePaymentProcessor = Substitute.For<IInvoicePrePaymentProcessor>();
    private readonly IPaymentMethodReader _paymentMethodReader = Substitute.For<IPaymentMethodReader>();
    private readonly ICommandSender _commandSender = Substitute.For<ICommandSender>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ILogger<DefaultAutoChargeService> _logger = NullLoggerFactory.Instance.CreateLogger<DefaultAutoChargeService>();

    private readonly DefaultAutoChargeService _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();

    public DefaultAutoChargeServiceTests()
    {
        _currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        _sut = new DefaultAutoChargeService(
            _prePaymentProcessor,
            _paymentMethodReader,
            _commandSender,
            _currentTenant,
            _logger);
    }

    private static InvoiceFinalizedEto CreateEto(
        CollectionMethod collectionMethod = CollectionMethod.Auto,
        decimal total = 100m) =>
        new(InvoiceId, TenantId, total, "EUR", collectionMethod);

    private static PaymentMethod CreateDefaultPaymentMethod() =>
        PaymentMethod.Create(Guid.NewGuid(), TenantId, "card", "stripe", "pm_123", "Visa **** 4242");

    // ======== Happy Path ========

    [Fact]
    public async Task HandleAsync_AutoCollectionWithRemainingAmount_ShouldInitiatePayment()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto();
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(80m));

        PaymentMethod method = CreateDefaultPaymentMethod();
        _paymentMethodReader.GetDefaultForTenantAsync(TenantId, ct).Returns(method);

        await _sut.HandleAsync(eto, ct);

        await _commandSender.Received(1)
            .SendAsync(Arg.Is<InitiatePaymentCommand>(c =>
                c.InvoiceId == InvoiceId
                && c.TenantId == TenantId
                && c.Amount == 80m
                && c.Currency == "EUR"
                && c.MethodType == "card"
                && c.ProviderName == "stripe"), ct);
    }

    // ======== Manual Collection ========

    [Fact]
    public async Task HandleAsync_ManualCollection_ShouldNotInitiatePayment()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto(CollectionMethod.SendInvoice);
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(100m));

        await _sut.HandleAsync(eto, ct);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<InitiatePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Fully Covered by Credit ========

    [Fact]
    public async Task HandleAsync_RemainingAmountZero_ShouldNotInitiatePayment()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto();
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(0m));

        await _sut.HandleAsync(eto, ct);

        await _paymentMethodReader.DidNotReceive()
            .GetDefaultForTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<InitiatePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NegativeRemainingAmount_ShouldNotInitiatePayment()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto();
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(-5m));

        await _sut.HandleAsync(eto, ct);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<InitiatePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== No Default Payment Method ========

    [Fact]
    public async Task HandleAsync_NoDefaultPaymentMethod_ShouldNotInitiatePayment()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto();
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(100m));
        _paymentMethodReader.GetDefaultForTenantAsync(TenantId, ct).Returns((PaymentMethod?)null);

        await _sut.HandleAsync(eto, ct);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<InitiatePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Tenant Context ========

    [Fact]
    public async Task HandleAsync_ShouldChangeTenantContext()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto(CollectionMethod.SendInvoice);
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(100m));

        await _sut.HandleAsync(eto, ct);

        _currentTenant.Received(1).Change(TenantId, Arg.Any<string?>());
    }

    // ======== Idempotency Key Format ========

    [Fact]
    public async Task HandleAsync_ShouldUseInvoiceIdBasedIdempotencyKey()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InvoiceFinalizedEto eto = CreateEto();
        _prePaymentProcessor.ProcessAsync(eto, ct).Returns(new PrePaymentResult(50m));

        PaymentMethod method = CreateDefaultPaymentMethod();
        _paymentMethodReader.GetDefaultForTenantAsync(TenantId, ct).Returns(method);

        await _sut.HandleAsync(eto, ct);

        await _commandSender.Received(1)
            .SendAsync(Arg.Is<InitiatePaymentCommand>(c =>
                c.IdempotencyKey == $"inv-{InvoiceId:N}"), ct);
    }
}
