using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;
using Granit.Invoicing;
using Granit.Invoicing.BackgroundJobs.Internal;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.BackgroundJobs.Tests.Internal;

public sealed class DefaultOverdueInvoiceDetectionServiceTests
{
    // ======== Fixtures ========

    private readonly IInvoiceReader _invoiceReader = Substitute.For<IInvoiceReader>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ILogger<DefaultOverdueInvoiceDetectionService> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultOverdueInvoiceDetectionService>();

    private readonly DefaultOverdueInvoiceDetectionService _sut;
    private static readonly DateTimeOffset Now = new(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);

    public DefaultOverdueInvoiceDetectionServiceTests()
    {
        _clock.Now.Returns(Now);
        _currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        _sut = new DefaultOverdueInvoiceDetectionService(
            _invoiceReader,
            _distributedEventBus,
            _clock,
            _currentTenant,
            _logger);
    }

    private static Invoice CreateOverdueInvoice(Guid? tenantId = null)
    {
        Guid tid = tenantId ?? Guid.NewGuid();
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            tid,
            ContactId.Create(Guid.NewGuid()),
            InvoiceDocumentType.Invoice,
            "EUR",
            CollectionMethod.Auto,
            BillingReason.SubscriptionCycle);

        invoice.AddLineItem(InvoiceLineItem.Create(
            Guid.NewGuid(), "Test item", 1m, 100m,
            new Domain.ValueObjects.LineItemSource(InvoiceSourceType.Subscription, Guid.NewGuid().ToString())));

        invoice.Finalize("INV-001", Now.AddDays(-35), dueAt: Now.AddDays(-5));
        return invoice;
    }

    // ======== Happy Path: Single Overdue Invoice ========

    [Fact]
    public async Task DetectAsync_SingleOverdueInvoice_ShouldPublishEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Invoice invoice = CreateOverdueInvoice();
        _invoiceReader.GetOverdueAsync(Now, ct).Returns([invoice]);

        await _sut.DetectAsync(ct);

        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<InvoiceOverdueEto>(e =>
                e.InvoiceId == invoice.Id
                && e.TenantId == invoice.TenantId!.Value
                && e.DueAt == invoice.DueAt!.Value),
            Arg.Any<CancellationToken>());
    }

    // ======== Multiple Overdue Invoices ========

    [Fact]
    public async Task DetectAsync_MultipleOverdueInvoices_ShouldPublishEventForEach()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Invoice invoice1 = CreateOverdueInvoice();
        Invoice invoice2 = CreateOverdueInvoice();
        _invoiceReader.GetOverdueAsync(Now, ct).Returns([invoice1, invoice2]);

        await _sut.DetectAsync(ct);

        await _distributedEventBus.Received(2).PublishAsync(Arg.Any<InvoiceOverdueEto>(), Arg.Any<CancellationToken>());
    }

    // ======== No Overdue Invoices ========

    [Fact]
    public async Task DetectAsync_NoOverdueInvoices_ShouldNotPublish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _invoiceReader.GetOverdueAsync(Now, ct).Returns([]);

        await _sut.DetectAsync(ct);

        await _distributedEventBus.DidNotReceive().PublishAsync(Arg.Any<InvoiceOverdueEto>(), Arg.Any<CancellationToken>());
    }

    // ======== Tenant Context Switching ========

    [Fact]
    public async Task DetectAsync_ShouldSwitchTenantForEachInvoice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        Invoice invoice1 = CreateOverdueInvoice(tenant1);
        Invoice invoice2 = CreateOverdueInvoice(tenant2);
        _invoiceReader.GetOverdueAsync(Now, ct).Returns([invoice1, invoice2]);

        await _sut.DetectAsync(ct);

        _currentTenant.Received(1).Change(tenant1, Arg.Any<string?>());
        _currentTenant.Received(1).Change(tenant2, Arg.Any<string?>());
    }

    // ======== Clock Used for Query ========

    [Fact]
    public async Task DetectAsync_ShouldUseClockNowForQuery()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _invoiceReader.GetOverdueAsync(Now, ct).Returns([]);

        await _sut.DetectAsync(ct);

        await _invoiceReader.Received(1).GetOverdueAsync(Now, ct);
    }
}
