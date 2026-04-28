using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.EntityFrameworkCore.Internal;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres integration tests for <see cref="InvoicePartyReferenceRewriter"/>. The bulk
/// <c>ExecuteUpdateAsync</c> path requires a relational provider, so the EF in-memory
/// provider can't substitute here.
/// </summary>
public sealed class InvoicePartyReferenceRewriterPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestInvoicingDbContextFactory _factory = null!;
    private InvoicePartyReferenceRewriter _sut = null!;

    public InvoicePartyReferenceRewriterPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _factory = new TestInvoicingDbContextFactory(_postgres.ConnectionString);
        await using InvoicingDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        string p = GranitInvoicingDbProperties.DbTablePrefix;
        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {p}invoice_external_references, {p}invoice_documents, {p}invoice_line_items, {p}invoices RESTART IDENTITY CASCADE;",
            TestContext.Current.CancellationToken);

        _sut = new InvoicePartyReferenceRewriter(_factory);
    }

    public ValueTask DisposeAsync() => _factory?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task RewriteAsync_RedirectsLoserInvoicesOntoSurvivor()
    {
        var survivorId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();

        await SeedAsync(
            NewInvoice(loserId),
            NewInvoice(loserId),
            NewInvoice(survivorId),
            NewInvoice(otherPartyId));

        int rewritten = await _sut.RewriteAsync(survivorId, loserId, TestContext.Current.CancellationToken);
        rewritten.ShouldBe(2);

        await using InvoicingDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var survivorPartyId = PartyId.Create(survivorId);
        int survivorCount = await db.Invoices
            .CountAsync(i => i.PartyId == survivorPartyId, TestContext.Current.CancellationToken);
        survivorCount.ShouldBe(3);

        var loserPartyId = PartyId.Create(loserId);
        int loserCount = await db.Invoices
            .CountAsync(i => i.PartyId == loserPartyId, TestContext.Current.CancellationToken);
        loserCount.ShouldBe(0);

        var otherPartyIdValueObject = PartyId.Create(otherPartyId);
        int otherCount = await db.Invoices
            .CountAsync(i => i.PartyId == otherPartyIdValueObject, TestContext.Current.CancellationToken);
        otherCount.ShouldBe(1);
    }

    [Fact]
    public async Task RewriteAsync_RedirectsFinalisedInvoices_TooByDesign()
    {
        var survivorId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        await SeedAsync(
            NewInvoice(loserId),
            NewInvoice(loserId, finalise: true));

        int rewritten = await _sut.RewriteAsync(survivorId, loserId, TestContext.Current.CancellationToken);
        rewritten.ShouldBe(2);

        await using InvoicingDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var survivorPartyId = PartyId.Create(survivorId);
        List<InvoiceStatus> statuses = await db.Invoices
            .Where(i => i.PartyId == survivorPartyId)
            .Select(i => i.Status)
            .ToListAsync(TestContext.Current.CancellationToken);
        statuses.ShouldContain(InvoiceStatus.Draft);
        statuses.ShouldContain(InvoiceStatus.Open);
    }

    [Fact]
    public async Task RewriteAsync_ReturnsZero_WhenLoserHasNoInvoices()
    {
        int rewritten = await _sut.RewriteAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);
        rewritten.ShouldBe(0);
    }

    [Fact]
    public async Task CountAsync_ReturnsLoserInvoiceCount_WithoutMutating()
    {
        var loserId = Guid.NewGuid();
        await SeedAsync(NewInvoice(loserId), NewInvoice(loserId), NewInvoice(loserId));

        int count = await _sut.CountAsync(Guid.NewGuid(), loserId, TestContext.Current.CancellationToken);
        count.ShouldBe(3);

        await using InvoicingDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var loserPartyId = PartyId.Create(loserId);
        int still = await db.Invoices.CountAsync(i => i.PartyId == loserPartyId, TestContext.Current.CancellationToken);
        still.ShouldBe(3);
    }

    private async Task SeedAsync(params Invoice[] invoices)
    {
        await using InvoicingDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Invoice NewInvoice(Guid partyId, bool finalise = false)
    {
        var invoice = Invoice.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            partyId: PartyId.Create(partyId),
            documentType: InvoiceDocumentType.Invoice,
            currency: "EUR",
            collectionMethod: CollectionMethod.Auto,
            billingReason: BillingReason.SubscriptionCycle);

        invoice.AddLineItem(InvoiceLineItem.Create(
            id: Guid.NewGuid(),
            description: "Test line",
            quantity: 1,
            unitPrice: 10.00m,
            source: new LineItemSource(InvoiceSourceType.OneShot, Guid.NewGuid().ToString())));

        if (finalise)
        {
            invoice.Finalize(
                documentNumber: "INV-" + Guid.NewGuid().ToString("N")[..8],
                issuedAt: DateTimeOffset.UtcNow,
                dueAt: DateTimeOffset.UtcNow.AddDays(30));
        }

        return invoice;
    }
}
