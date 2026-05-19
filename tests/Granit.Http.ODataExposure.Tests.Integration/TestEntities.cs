using Granit.DataExchange.Export;
using Granit.Domain;
using Granit.Entities;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Test invoice entity carrying every cross-tenant attack target: a TenantId
/// (the framework's required filter) and an IsDeleted soft-delete flag. The
/// hostile <c>$filter</c> URL parameter targets these very columns;
/// <c>ApplyGranitConventions</c> on the test DbContext is what enforces the
/// AND-prefix.
/// </summary>
internal sealed class Invoice : IMultiTenant, ISoftDeletable
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; set; }
    public string Number { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

internal sealed class TestDbContext(
    DbContextOptions<TestDbContext> options,
    ICurrentTenant? currentTenant = null) : DbContext(options)
{
    private readonly ICurrentTenant? _currentTenant = currentTenant;

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Number).HasMaxLength(64).IsRequired();
        });

        // ApplyGranitConventions wires the multi-tenancy filter
        // (WHERE TenantId = currentTenant.Id) AND the soft-delete filter
        // (WHERE IsDeleted = false). These filters are appended to every
        // query by EF Core BEFORE any user-supplied predicate; that's the
        // load-bearing guarantee this integration test pins.
        modelBuilder.ApplyGranitConventions(_currentTenant);
    }
}

internal sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
{
    public override string Name => "Test.Invoices";

    protected override void Configure(QueryDefinitionBuilder<Invoice> builder) =>
        builder
            .Column(i => i.Number, c => c.Filterable())
            .Column(i => i.Amount, c => c.Filterable())
            .Column(i => i.TenantId, c => c.Filterable())  // intentionally exposed: hostile $filter targets this column
            .DefaultPageSize(50);
}

internal sealed class InvoiceSource(TestDbContext db) : IQueryableSource<Invoice>
{
    private readonly TestDbContext _db = db;
    public IQueryable<Invoice> GetQueryable() => _db.Invoices.AsNoTracking();
}

/// <summary>
/// Per ADR-050, OData EntitySets require a registered EntityDefinition that
/// references an ExportDefinition. The integration suites use the simplest
/// possible pair to land the v1 contract — Number, Amount, TenantId scalars
/// (the latter intentionally exposed because hostile <c>$filter</c> targets
/// it; tenant isolation is enforced by the framework filter, not by hiding
/// the column).
/// </summary>
internal sealed class InvoiceExportDefinition : ExportDefinition<Invoice>
{
    public override string Name => "Test.InvoiceExport";

    protected override void Configure(ExportDefinitionBuilder<Invoice> builder) =>
        builder
            .Field(i => i.Number)
            .Field(i => i.Amount)
            .Field(i => i.TenantId);
}

internal sealed class InvoiceEntityDefinition : EntityDefinition<Invoice>
{
    public override string Name => "Test.Invoice";

    protected override void Configure(EntityDefinitionBuilder<Invoice> builder) =>
        builder
            .Query<InvoiceQueryDefinition>()
            .Export<InvoiceExportDefinition>();
}
