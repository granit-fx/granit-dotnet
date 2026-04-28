using Granit.Analytics.EntityFrameworkCore;
using Granit.Analytics.EntityFrameworkCore.Extensions;
using Granit.Analytics.Extensions;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.EntityFrameworkCore.Internal;
using Granit.Invoicing.Metrics;
using Granit.Invoicing.Queries;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Options;
using Granit.Testing.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Reference-impl tests for the two metrics shipped under
/// <c>Granit.Invoicing/Metrics/</c> (story #1377). Exercises:
/// <list type="bullet">
///   <item>BaseFilter pruning to <see cref="InvoiceStatus.Open"/> only.</item>
///   <item>Sum of <see cref="Invoice.AmountRemaining"/> (not <c>Total</c>) so partially paid
///     invoices contribute only their unpaid balance.</item>
///   <item>Tenant isolation — each tenant sees its own invoices, never another tenant's.</item>
///   <item>Empty-set semantics on a fresh tenant (count → 0, total → 0).</item>
/// </list>
/// </summary>
public sealed class UnpaidInvoiceMetricsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 4, 28, 0, 0, 0, TimeSpan.Zero);

    private SqliteConnection _connection = null!;
    private ICurrentTenant _currentTenant = null!;
    private InvoicingDbContext _db = null!;
    private ServiceProvider _provider = null!;
    private IMetricExecutor<Invoice, int> _countExecutor = null!;
    private IMetricExecutor<Invoice, decimal> _totalExecutor = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        // Required: ApplyGranitConventions captures the ICurrentTenant instance inside the
        // multi-tenant query-filter expression, and EF Core's process-wide model cache
        // pins the first instance it sees. See the helper's XML doc for the full story.
        DbContextOptions<InvoicingDbContext> options = new DbContextOptionsBuilder<InvoicingDbContext>()
            .UseSqlite(_connection)
            .EnableGranitTestModelIsolation()
            .Options;

        _currentTenant = Substitute.For<ICurrentTenant>();
        _currentTenant.IsAvailable.Returns(true);
        _db = new InvoicingDbContext(options, _currentTenant, dataFilter: null);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Build the DI container for IQueryEngine<Invoice> + IMetricExecutor<Invoice, *>.
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddOptions<QueryEngineOptions>();
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QueryEngineOptions>>().Value);
        services.AddGranitQueryEngine();
        services.AddScoped(typeof(IQueryEngine<>),
            typeof(QueryEngine.EntityFrameworkCore.GranitQueryEngineEntityFrameworkCoreModule)
                .Assembly
                .GetType("Granit.QueryEngine.EntityFrameworkCore.Internal.QueryEngine`1", throwOnError: true)!);
        services.AddQueryDefinition<Invoice, InvoiceQueryDefinition>();

        services.AddGranitAnalytics();
        services.AddGranitAnalyticsEntityFrameworkCore();

        _provider = services.BuildServiceProvider();
        _countExecutor = _provider.GetRequiredService<IMetricExecutor<Invoice, int>>();
        _totalExecutor = _provider.GetRequiredService<IMetricExecutor<Invoice, decimal>>();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private static Invoice OpenInvoice(string number, Guid tenantId, decimal subtotal, decimal amountPaid = 0m)
    {
        var inv = Invoice.Create(
            Guid.NewGuid(), tenantId, PartyId.Create(Guid.NewGuid()),
            InvoiceDocumentType.Invoice, "EUR",
            CollectionMethod.Auto, BillingReason.SubscriptionCycle);
        inv.AddLineItem(InvoiceLineItem.Create(
            Guid.NewGuid(), "test line", quantity: 1m, unitPrice: subtotal,
            source: new LineItemSource(InvoiceSourceType.OneShot)));
        inv.Finalize(number, issuedAt: Now, dueAt: Now.AddDays(30));
        if (amountPaid > 0)
        {
            inv.RecordPayment(amountPaid, paidAt: Now, tolerance: 0m);
        }
        return inv;
    }

    private static Invoice DraftInvoice(Guid tenantId) =>
        Invoice.Create(
            Guid.NewGuid(), tenantId, PartyId.Create(Guid.NewGuid()),
            InvoiceDocumentType.Invoice, "EUR",
            CollectionMethod.Auto, BillingReason.SubscriptionCycle);

    private static Invoice PaidInvoice(string number, Guid tenantId, decimal subtotal)
    {
        // Open with the full amount paid → state machine transitions to Paid.
        return OpenInvoice(number, tenantId, subtotal, amountPaid: subtotal);
    }

    private static Invoice VoidInvoice(string number, Guid tenantId, decimal subtotal)
    {
        Invoice inv = OpenInvoice(number, tenantId, subtotal);
        inv.VoidInvoice();
        return inv;
    }

    [Fact]
    public async Task UnpaidInvoiceCount_OnlyCountsOpenStatus()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.Id.Returns(tenantId);

        // 3 Open + 1 Paid + 1 Void + 1 Draft = only 3 should count.
        _db.Invoices.Add(OpenInvoice("INV-1", tenantId, 100m));
        _db.Invoices.Add(OpenInvoice("INV-2", tenantId, 200m));
        _db.Invoices.Add(OpenInvoice("INV-3", tenantId, 300m));
        _db.Invoices.Add(PaidInvoice("INV-4", tenantId, 400m));
        _db.Invoices.Add(VoidInvoice("INV-5", tenantId, 500m));
        _db.Invoices.Add(DraftInvoice(tenantId));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        int? result = await _countExecutor.ExecuteAsync(
            new UnpaidInvoiceCountMetricDefinition(),
            _db.Invoices,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        result.ShouldBe(3);
    }

    [Fact]
    public async Task UnpaidInvoiceTotal_SumsAmountRemainingNotTotal()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.Id.Returns(tenantId);

        // Two Open invoices: one fully unpaid, one partially paid.
        // Subtotal 1000, AmountPaid 600 → AmountRemaining 400.
        // Subtotal 500, AmountPaid 0   → AmountRemaining 500.
        _db.Invoices.Add(OpenInvoice("INV-1", tenantId, 1000m, amountPaid: 600m));
        _db.Invoices.Add(OpenInvoice("INV-2", tenantId, 500m));
        // Paid invoice: AmountRemaining = 0 anyway, AND filtered out by BaseFilter.
        _db.Invoices.Add(PaidInvoice("INV-3", tenantId, 700m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        decimal? result = await _totalExecutor.ExecuteAsync(
            new UnpaidInvoiceTotalMetricDefinition(),
            _db.Invoices,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        // Sum of AmountRemaining over Open: 400 + 500 = 900 (NOT 1000 + 500 = 1500 if we summed Total).
        result.ShouldBe(900m);
    }

    [Fact]
    public async Task EmptyDataset_Count_ReturnsZero()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.Id.Returns(tenantId);

        int? result = await _countExecutor.ExecuteAsync(
            new UnpaidInvoiceCountMetricDefinition(),
            _db.Invoices,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task EmptyDataset_Total_ReturnsZero()
    {
        _currentTenant.Id.Returns(Guid.NewGuid());

        // Sum over empty set = 0 (mathematical sum-of-empty), not null.
        decimal? result = await _totalExecutor.ExecuteAsync(
            new UnpaidInvoiceTotalMetricDefinition(),
            _db.Invoices,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task UnpaidInvoiceCount_BaseFilter_ExcludesAllNonOpenStatuses()
    {
        // Without BaseFilter, all 4 rows would count. With BaseFilter (Status == Open),
        // only the 1 Open row counts.
        var tenantId = Guid.NewGuid();
        _currentTenant.Id.Returns(tenantId);
        _db.Invoices.Add(OpenInvoice("INV-OPEN", tenantId, 100m));
        _db.Invoices.Add(PaidInvoice("INV-PAID", tenantId, 200m));
        _db.Invoices.Add(VoidInvoice("INV-VOID", tenantId, 300m));
        _db.Invoices.Add(DraftInvoice(tenantId));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        int? result = await _countExecutor.ExecuteAsync(
            new UnpaidInvoiceCountMetricDefinition(),
            _db.Invoices,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        result.ShouldBe(1);
    }

    [Fact]
    public async Task UnpaidInvoiceCount_RespectsTenantIsolation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _db.Invoices.Add(OpenInvoice("A1", tenantA, 100m));
        _db.Invoices.Add(OpenInvoice("A2", tenantA, 200m));
        _db.Invoices.Add(OpenInvoice("A3", tenantA, 300m));
        _db.Invoices.Add(OpenInvoice("B1", tenantB, 400m));
        _db.Invoices.Add(OpenInvoice("B2", tenantB, 500m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        ICurrentTenant tenantContextA = Substitute.For<ICurrentTenant>();
        tenantContextA.IsAvailable.Returns(true);
        tenantContextA.Id.Returns(tenantA);

        DbContextOptions<InvoicingDbContext> options = new DbContextOptionsBuilder<InvoicingDbContext>()
            .UseSqlite(_connection)
            .EnableGranitTestModelIsolation()
            .Options;
        await using var dbA = new InvoicingDbContext(options, tenantContextA, dataFilter: null);

        int? resultA = await _countExecutor.ExecuteAsync(
            new UnpaidInvoiceCountMetricDefinition(),
            dbA.Invoices,
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        resultA.ShouldBe(3);
    }
}
