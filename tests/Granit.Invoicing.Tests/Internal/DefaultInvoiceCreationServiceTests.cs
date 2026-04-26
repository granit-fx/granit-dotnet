using Granit.Contacts;
using Granit.Contacts.Domain;
using Granit.Guids;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.Dtos;
using Granit.Invoicing.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Tests.Internal;

public sealed class DefaultInvoiceCreationServiceTests
{
    // ======== Fixtures ========

    private readonly IInvoiceWriter _invoiceWriter = Substitute.For<IInvoiceWriter>();
    private readonly IContactReader _contactReader = Substitute.For<IContactReader>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDefaultContactResolver _defaultContactResolver = Substitute.For<IDefaultContactResolver>();
    private readonly ILogger<DefaultInvoiceCreationService> _logger = NullLoggerFactory.Instance.CreateLogger<DefaultInvoiceCreationService>();
    private readonly ITaxCalculator _taxCalculator = Substitute.For<ITaxCalculator>();
    private readonly IInvoiceNumberGenerator _numberGenerator = Substitute.For<IInvoiceNumberGenerator>();

    private static readonly DateTimeOffset Now = new(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ContactId = Guid.NewGuid();

    public DefaultInvoiceCreationServiceTests()
    {
        _clock.Now.Returns(Now);
        // Default behaviour: an explicit ContactId resolves to a contact with no billing address.
        // Individual tests override this when they need a contact with an address.
        var bareContact = Contact.Create(
            ContactId, null, ContactKind.Company, "Test Co", "EUR");
        _contactReader.GetByIdAsync(
            Arg.Any<Granit.Contacts.Domain.ValueObjects.ContactId>(),
            Arg.Any<CancellationToken>())
            .Returns(bareContact);
    }

    private DefaultInvoiceCreationService CreateSut(
        ITaxCalculator? taxCalculator = null,
        IInvoiceNumberGenerator? numberGenerator = null) =>
        new(
            _invoiceWriter,
            _contactReader,
            _guidGenerator,
            _clock,
            _defaultContactResolver,
            _logger,
            taxCalculator,
            numberGenerator);

    private static CreateInvoiceCommand CreateCommand(
        int lineItemCount = 1,
        DateTimeOffset? periodStart = null,
        DateTimeOffset? periodEnd = null) =>
        new(
            TenantId,
            "EUR",
            CollectionMethod.Auto,
            BillingReason.SubscriptionCycle,
            Enumerable.Range(0, lineItemCount)
                .Select(i => new CreateInvoiceLineItem(
                    $"Line item {i + 1}",
                    Quantity: 1m,
                    UnitPrice: 50m,
                    InvoiceSourceType.Subscription,
                    Guid.NewGuid().ToString()))
                .ToList(),
            ContactId: ContactId,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd);

    // ======== Happy Path: Basic Creation ========

    [Fact]
    public async Task CreateAsync_BasicCommand_ShouldPersistInvoice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var invoiceId = Guid.NewGuid();
        var lineItemId = Guid.NewGuid();
        _guidGenerator.Create().Returns(invoiceId, lineItemId);

        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand();

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv =>
                inv.Id == invoiceId
                && inv.Currency == "EUR"
                && inv.CollectionMethod == CollectionMethod.Auto
                && inv.BillingReason == BillingReason.SubscriptionCycle
                && inv.LineItems.Count == 1
                && inv.Status == InvoiceStatus.Draft),
            ct);
    }

    // ======== Multiple Line Items ========

    [Fact]
    public async Task CreateAsync_MultipleLineItems_ShouldAddAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand(lineItemCount: 3);

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv => inv.LineItems.Count == 3), ct);
    }

    // ======== Billing Period ========

    [Fact]
    public async Task CreateAsync_WithBillingPeriod_ShouldSetPeriod()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid());

        DateTimeOffset start = Now.AddDays(-30);
        DateTimeOffset end = Now;
        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand(periodStart: start, periodEnd: end);

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv =>
                inv.PeriodStart == start && inv.PeriodEnd == end), ct);
    }

    [Fact]
    public async Task CreateAsync_WithoutBillingPeriod_ShouldNotSetPeriod()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid());

        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand();

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv =>
                inv.PeriodStart == null && inv.PeriodEnd == null), ct);
    }

    // ======== With Number Generator (auto-finalize) ========

    [Fact]
    public async Task CreateAsync_WithNumberGenerator_ShouldFinalizeInvoice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid());
        _numberGenerator.GenerateNextAsync(InvoiceDocumentType.Invoice, TenantId, ct)
            .Returns("INV-2026-0001");

        DefaultInvoiceCreationService sut = CreateSut(numberGenerator: _numberGenerator);
        CreateInvoiceCommand command = CreateCommand();

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv =>
                inv.Status == InvoiceStatus.Open
                && inv.InvoiceNumber == "INV-2026-0001"
                && inv.IssuedAt == Now
                && inv.DueAt == Now.AddDays(30)), ct);
    }

    // ======== Without Number Generator (stays draft) ========

    [Fact]
    public async Task CreateAsync_WithoutNumberGenerator_ShouldStayDraft()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid());

        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand();

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv =>
                inv.Status == InvoiceStatus.Draft
                && inv.InvoiceNumber == null), ct);
    }

    // ======== Without Tax Calculator ========

    [Fact]
    public async Task CreateAsync_WithoutTaxCalculator_ShouldNotCalculateTax()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid());

        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand();

        await sut.CreateAsync(command, ct);

        await _invoiceWriter.Received(1).AddAsync(
            Arg.Is<Invoice>(inv => inv.TaxTotal == 0m), ct);
    }

    // ======== Tax Calculator Without Billing Address ========

    [Fact]
    public async Task CreateAsync_WithTaxCalculatorButNoBillingAddress_ShouldSkipTax()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid());

        DefaultInvoiceCreationService sut = CreateSut(taxCalculator: _taxCalculator);
        CreateInvoiceCommand command = CreateCommand();

        await sut.CreateAsync(command, ct);

        await _taxCalculator.DidNotReceive()
            .CalculateAsync(Arg.Any<TaxRequest>(), Arg.Any<CancellationToken>());
    }

    // ======== Guid Generator Called Per Entity ========

    [Fact]
    public async Task CreateAsync_ShouldCallGuidGeneratorForInvoiceAndEachLineItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _guidGenerator.Create().Returns(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        DefaultInvoiceCreationService sut = CreateSut();
        CreateInvoiceCommand command = CreateCommand(lineItemCount: 2);

        await sut.CreateAsync(command, ct);

        // 1 for invoice + 2 for line items = 3 calls
        _guidGenerator.Received(3).Create();
    }
}
