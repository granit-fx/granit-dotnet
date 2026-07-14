using Granit.DataExchange.Export;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Entities;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Test invoice entity carrying every cross-tenant attack target: a TenantId
/// (the framework's required filter) and an IsDeleted soft-delete flag. The
/// hostile <c>$filter</c> URL parameter targets these very columns;
/// <c>ApplyGranitConventions</c> on the test DbContext is what enforces the
/// AND-prefix. Since #3005 the invoice also carries a real
/// <see cref="Customer"/> navigation (with a nested <see cref="Address"/>)
/// so the recursive <c>$expand</c> whitelist, depth cap, and ADR-050
/// target-type minimization are exercised against actual SQL joins.
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
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// $expand target for the Invoice set. <see cref="InternalScore"/> is
/// deliberately NOT exported — per ADR-050 it must never surface in the EDM
/// nor in an expanded payload, even though it is a public property.
/// </summary>
internal sealed class Customer : IMultiTenant
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; set; }
    public string Name { get; init; } = string.Empty;
    public string? Email { get; init; }
    public int InternalScore { get; init; }
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }
}

/// <summary>Second-hop $expand target (Invoice → Customer → Address) for the depth + dotted-path suites. <see cref="Zip"/> is NOT exported.</summary>
internal sealed class Address
{
    public Guid Id { get; init; }
    public string City { get; init; } = string.Empty;
    public string? Zip { get; init; }
}

internal sealed class TestDbContext(
    DbContextOptions<TestDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
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
            e.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(128).IsRequired();
            e.Property(c => c.Email).HasMaxLength(256);
            e.HasOne(c => c.Address).WithMany().HasForeignKey(c => c.AddressId);
        });

        modelBuilder.Entity<Address>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.City).HasMaxLength(128).IsRequired();
            e.Property(a => a.Zip).HasMaxLength(16);
        });
    }
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

/// <summary>
/// #3005 — every type reachable through a whitelisted <c>$expand</c> path
/// needs an export-derived scalar whitelist. Name + Email are exported;
/// InternalScore is not and must stay out of $metadata AND payloads.
/// </summary>
internal sealed class CustomerExportDefinition : ExportDefinition<Customer>
{
    public override string Name => "Test.CustomerExport";

    protected override void Configure(ExportDefinitionBuilder<Customer> builder) =>
        builder
            .Field(c => c.Name)
            .Field(c => c.Email);
}

internal sealed class AddressExportDefinition : ExportDefinition<Address>
{
    public override string Name => "Test.AddressExport";

    protected override void Configure(ExportDefinitionBuilder<Address> builder) =>
        builder.Field(a => a.City);
}
