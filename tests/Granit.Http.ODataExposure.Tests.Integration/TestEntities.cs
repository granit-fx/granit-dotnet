using Granit.DataExchange.Export;
using Granit.Entities;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;

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
    public string Status { get; init; } = "Draft";
    public DateTimeOffset? PaidAt { get; init; }
    public string? InternalNote { get; init; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

internal sealed class TestDbContext(
    DbContextOptions<TestDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder) =>
        // This override only configures the entity surface. GranitDbContext's sealed
        // OnModelCreating then wires the parameterised multi-tenant filter (built inside a
        // member of this context — CurrentTenantId — so EF Core emits @ef_filter__CurrentTenantId
        // re-bound per request instead of constant-folding a frozen value) and runs
        // ApplyGranitConventions for the remaining filters (soft-delete WHERE IsDeleted = false,
        // etc.). All filters compose BEFORE any user-supplied predicate — the guarantee this suite pins.
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Number).HasMaxLength(64).IsRequired();
            e.Property(i => i.Status).HasMaxLength(32).IsRequired();
            e.Property(i => i.InternalNote).HasMaxLength(256);
        });
}

internal sealed class InvoiceQueryDefinition : QueryDefinition<Invoice>
{
    public override string Name => "Test.Invoices";

    protected override void Configure(QueryDefinitionBuilder<Invoice> builder) =>
        builder
            // Sortable() drives the #3004 $orderby whitelist (AllowedOrderByProperties) —
            // TenantIsolationTests order by TenantId, so the isolation columns are sortable.
            .Column(i => i.Number, c => c.Filterable().Sortable())
            .Column(i => i.Amount, c => c.Filterable().Sortable())
            .Column(i => i.TenantId, c => c.Filterable().Sortable())  // intentionally exposed: hostile $filter targets this column
            .Column(i => i.Status, c => c.Filterable())               // filterable but NOT sortable — $orderby=Status must 400
            .Column(i => i.PaidAt, c => c.Filterable())
            .Column(i => i.InternalNote)                              // declared but NOT filterable — #3004 strict rejection target
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
            .Field(i => i.TenantId)
            .Field(i => i.Status)
            .Field(i => i.PaidAt)
            // EDM-visible but not Filterable() in the QueryDefinition — pins the #3004
            // breaking change: $filter=InternalNote ... used to pass through ApplyTo
            // silently, it now 400s from the engine's strict predicate validation.
            .Field(i => i.InternalNote);
}

internal sealed class InvoiceEntityDefinition : EntityDefinition<Invoice>
{
    public override string Name => "Test.Invoice";

    protected override void Configure(EntityDefinitionBuilder<Invoice> builder) =>
        builder
            .Query<InvoiceQueryDefinition>()
            .Export<InvoiceExportDefinition>();
}
