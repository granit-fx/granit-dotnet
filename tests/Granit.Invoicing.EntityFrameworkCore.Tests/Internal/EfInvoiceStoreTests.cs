using Granit.DataFiltering;
using Granit.Domain;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.EntityFrameworkCore.Tests.Internal;

[Collection(InvoicingDbSerialGroup.Name)]
public sealed class EfInvoiceStoreTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestFactory _factory;
    private readonly EfInvoiceStore _store;

    public EfInvoiceStoreTests()
    {
        DbContextOptions<InvoicingDbContext> options = new DbContextOptionsBuilder<InvoicingDbContext>()
            .UseInMemoryDatabase($"invoicing-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _store = new EfInvoiceStore(_factory, _factory.Tenant);
    }

    public async ValueTask DisposeAsync()
    {
        await using InvoicingDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private static Invoice NewDraftInvoice(Guid? tenantId = null, Guid? contactId = null) =>
        Invoice.Create(
            Guid.NewGuid(), tenantId ?? Guid.NewGuid(),
            PartyId.Create(contactId ?? Guid.NewGuid()),
            InvoiceDocumentType.Invoice, "EUR",
            CollectionMethod.Auto, BillingReason.SubscriptionCycle);

    private static Invoice NewOpenInvoice(string number, Guid? tenantId = null, DateTimeOffset? dueAt = null)
    {
        Invoice inv = NewDraftInvoice(tenantId);
        inv.Finalize(number, issuedAt: Now, dueAt: dueAt ?? Now.AddDays(30));
        return inv;
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrips()
    {
        Invoice inv = NewOpenInvoice("INV-001");
        await ((IInvoiceWriter)_store).AddAsync(inv, TestContext.Current.CancellationToken);

        Invoice? loaded = await _store.GetByIdAsync(
            InvoiceId.Create(inv.Id), TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.InvoiceNumber.ShouldBe("INV-001");
        loaded.Status.ShouldBe(InvoiceStatus.Open);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        Invoice? loaded = await _store.GetByIdAsync(
            InvoiceId.Create(Guid.NewGuid()), TestContext.Current.CancellationToken);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task GetByNumberAsync_MatchesByInvoiceNumber()
    {
        Invoice inv = NewOpenInvoice("INV-042");
        await ((IInvoiceWriter)_store).AddAsync(inv, TestContext.Current.CancellationToken);

        Invoice? hit = await _store.GetByNumberAsync("INV-042", TestContext.Current.CancellationToken);
        Invoice? miss = await _store.GetByNumberAsync("INV-MISS", TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        miss.ShouldBeNull();
    }

    [Fact]
    public async Task GetForTenantAsync_FiltersByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await ((IInvoiceWriter)_store).AddAsync(NewOpenInvoice("A1", tenantA), TestContext.Current.CancellationToken);
        await ((IInvoiceWriter)_store).AddAsync(NewOpenInvoice("A2", tenantA), TestContext.Current.CancellationToken);
        await ((IInvoiceWriter)_store).AddAsync(NewOpenInvoice("B1", tenantB), TestContext.Current.CancellationToken);

        IReadOnlyList<Invoice> result = await _store.GetForTenantAsync(tenantA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(i => i.TenantId == tenantA);
    }

    [Fact]
    public async Task GetOverdueAsync_ReturnsOnlyOpenAndDue()
    {
        Invoice overdue = NewOpenInvoice("OD-1", dueAt: Now.AddDays(-1));
        Invoice notDue = NewOpenInvoice("OD-2", dueAt: Now.AddDays(30));
        await ((IInvoiceWriter)_store).AddAsync(overdue, TestContext.Current.CancellationToken);
        await ((IInvoiceWriter)_store).AddAsync(notDue, TestContext.Current.CancellationToken);

        IReadOnlyList<Invoice> result = await _store.GetOverdueAsync(Now, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(overdue.Id);
    }

    [Fact]
    public async Task GetCreditNotesForInvoiceAsync_FiltersByParent()
    {
        Invoice parent = NewOpenInvoice("INV-100");
        await ((IInvoiceWriter)_store).AddAsync(parent, TestContext.Current.CancellationToken);

        var creditNote = Invoice.Create(
            Guid.NewGuid(), parent.TenantId!.Value,
            parent.PartyId,
            InvoiceDocumentType.CreditNote, "EUR",
            CollectionMethod.Auto, BillingReason.Manual,
            creditNoteInfo: new CreditNoteInfo(InvoiceId.Create(parent.Id), "test refund"));
        creditNote.Finalize("CN-100-1", Now, dueAt: null);
        await ((IInvoiceWriter)_store).AddAsync(creditNote, TestContext.Current.CancellationToken);

        IReadOnlyList<Invoice> result = await _store.GetCreditNotesForInvoiceAsync(
            InvoiceId.Create(parent.Id), TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(creditNote.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        Invoice inv = NewDraftInvoice();
        await ((IInvoiceWriter)_store).AddAsync(inv, TestContext.Current.CancellationToken);

        await ((IInvoiceWriter)_store).DeleteAsync(inv, TestContext.Current.CancellationToken);

        Invoice? loaded = await _store.GetByIdAsync(
            InvoiceId.Create(inv.Id), TestContext.Current.CancellationToken);
        loaded.ShouldBeNull();
    }

    private sealed class TestFactory(DbContextOptions<InvoicingDbContext> options)
        : IDbContextFactory<InvoicingDbContext>
    {
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();
        private readonly IDataFilter _filter = new DataFilter();

        public InvoicingDbContext CreateDbContext() => new(options, Tenant, _filter);

        public Task<InvoicingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new InvoicingDbContext(options, Tenant, _filter));
    }
}

[CollectionDefinition(InvoicingDbSerialGroup.Name, DisableParallelization = true)]
public sealed class InvoicingDbSerialGroup
{
    public const string Name = "Invoicing-Db-serial";
}
