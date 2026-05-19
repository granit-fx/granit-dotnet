// =============================================================================
// Regression — GranitDbContext model-build order must finalise SVO mapping
// AFTER the multi-tenant filter step
// =============================================================================
// `GranitDbContext.OnModelCreating` calls `Entity<TEntity>().HasQueryFilter(...)`
// for every `IMultiTenant` type. That re-fires EF Core's navigation discovery
// for the entity, which re-adds any `SingleValueObject<T>` CLR property as a
// phantom navigation entity — even when the derived configuration already
// registered the property as a scalar via `builder.Property(e => e.X)`.
//
// If `ApplyGranitConventions` (which performs the SVO removal + converter
// pass) runs BEFORE that filter loop, the cleanup is undone and model
// validation fails:
//
//   System.InvalidOperationException :
//     The 'PartyId' property 'BalanceAccount.PartyId' could not be mapped
//     because the database provider does not support this type.
//
// Reported by granit-showcase during the multi-tenant filter migration on
// host-aggregating DbContexts that co-locate `IMultiTenant` and
// `SingleValueObject<T>` properties (e.g. `BalanceAccount` with `PartyId`,
// `Subscription` with `PlanId`).
//
// The fix runs the multi-tenant filter BEFORE `ApplyGranitConventions` so the
// SVO removal + converter pass is the LAST operation to touch the model surface.
//
// Provider note — the production crash surfaced via `NpgsqlModelValidator`, but
// `ThrowPropertyNotMappedException` lives in the abstract `RelationalModelValidator`
// base class, so the bug is provider-agnostic. SQLite-in-memory is therefore a
// faithful repro and avoids the test fixture overhead of a Postgres testcontainer.
// =============================================================================

using Granit.DataFiltering;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class GranitDbContextSvoMultiTenantOrderingTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public void Model_EntityWithMultiTenantAndSvoProperty_MapsSvoAsScalarColumn()
    {
        MutableTenant tenant = new() { Id = Guid.NewGuid() };

        using OrderingReproDbContext context = new(BuildOpts(), tenant);

        IEntityType owner = context.Model.FindEntityType(typeof(MultiTenantSvoOwner))!;
        IProperty? svoProperty = owner.FindProperty(nameof(MultiTenantSvoOwner.PartyId));

        svoProperty.ShouldNotBeNull(
            "SVO property must survive as a scalar on the IMultiTenant owner");

        svoProperty.GetValueConverter().ShouldNotBeNull(
            "SVO scalar property must carry the SingleValueObject<T> value converter " +
            "applied by ApplyGranitConventions");

        svoProperty.GetValueConverter()!.ProviderClrType.ShouldBe(typeof(Guid),
            "the SVO converter must produce the underlying primitive column type so the " +
            "database provider sees a Guid, not the SingleValueObject<T> subclass");

        context.Model.FindEntityType(typeof(ReproPartyId)).ShouldBeNull(
            "the SVO type must not survive as an auto-discovered entity type");
    }

    [Fact]
    public void Model_AutoDiscoveredSvoProperty_OnMultiTenantOwner_SurvivesAsScalar()
    {
        MutableTenant tenant = new() { Id = Guid.NewGuid() };

        using OrderingReproDbContext context = new(BuildOpts(), tenant);

        IEntityType owner = context.Model.FindEntityType(typeof(MultiTenantAutoDiscoveryOwner))!;
        IProperty? svoProperty = owner.FindProperty(nameof(MultiTenantAutoDiscoveryOwner.PartyId));

        svoProperty.ShouldNotBeNull(
            "auto-discovered SVO property must survive as a scalar after the multi-tenant " +
            "filter loop re-fires navigation discovery on the IMultiTenant owner");

        svoProperty.GetValueConverter().ShouldNotBeNull(
            "SVO converter must be reapplied after the multi-tenant filter step — " +
            "this is the load-bearing assertion for the ordering fix");

        context.Model.FindEntityType(typeof(ReproPartyId)).ShouldBeNull(
            "phantom SVO entity type must not survive the final cleanup pass");
    }

    [Fact]
    public void Model_StringBackedSvoProperty_OnMultiTenantOwner_MapsAsScalarColumn()
    {
        // Locks in that the SVO removal + converter machinery is generic over T —
        // the ordering bug surfaces identically for SingleValueObject<string>
        // (BlobReference) as it does for SingleValueObject<Guid> (PartyId).
        MutableTenant tenant = new() { Id = Guid.NewGuid() };

        using OrderingReproDbContext context = new(BuildOpts(), tenant);

        IEntityType owner = context.Model.FindEntityType(typeof(MultiTenantBlobOwner))!;
        IProperty? svoProperty = owner.FindProperty(nameof(MultiTenantBlobOwner.BlobRef));

        svoProperty.ShouldNotBeNull(
            "string-backed SVO property must survive as a scalar on the IMultiTenant owner");

        svoProperty.GetValueConverter().ShouldNotBeNull();

        svoProperty.GetValueConverter()!.ProviderClrType.ShouldBe(typeof(string),
            "the SVO converter must produce the underlying primitive column type — " +
            "string for BlobReference : SingleValueObject<string>");

        context.Model.FindEntityType(typeof(BlobReference)).ShouldBeNull(
            "the string-backed SVO type must not survive as an auto-discovered entity type");
    }

    [Fact]
    public async Task SaveAndQuery_RoundTripsSvoColumnThroughTheConverter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyGuid = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantId };

        await using (OrderingReproDbContext seed = new(BuildOpts(), tenant))
        {
            await seed.Database.EnsureCreatedAsync(ct);
            seed.Owners.Add(new MultiTenantSvoOwner
            {
                TenantId = tenantId,
                PartyId = ReproPartyId.Create(partyGuid),
            });
            await seed.SaveChangesAsync(ct);
        }

        await using OrderingReproDbContext query = new(BuildOpts(), tenant);
        MultiTenantSvoOwner loaded = await query.Owners.SingleAsync(ct);

        loaded.PartyId.Value.ShouldBe(partyGuid);
        loaded.TenantId.ShouldBe(tenantId);
    }

    private DbContextOptions<OrderingReproDbContext> BuildOpts()
        => new DbContextOptionsBuilder<OrderingReproDbContext>()
            .UseSqlite(_connection)
            .Options;

    private sealed class MutableTenant : ICurrentTenant
    {
        public bool IsAvailable => Id.HasValue;
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public IDisposable Change(Guid? id, string? name = null)
            => throw new NotSupportedException();
    }

    public sealed class ReproPartyId : SingleValueObject<Guid>
    {
        public override required Guid Value { get; init; }

        public static ReproPartyId Create(Guid value) => new() { Value = value };
    }

    public sealed class MultiTenantSvoOwner : IMultiTenant
    {
        public int Id { get; set; }
        public Guid? TenantId { get; set; }
        public ReproPartyId PartyId { get; set; } = null!;
    }

    // Mirrors BalanceAccountConfiguration in granit-business: explicit scalar
    // registration of the SVO property so EF Core's auto-discovery does not
    // treat it as a navigation. The SingleValueObject<T> converter is wired
    // by ApplyGranitConventions, but only if the property survives every
    // subsequent Entity<T>() call that re-fires navigation discovery.
    private sealed class MultiTenantSvoOwnerConfiguration
        : IEntityTypeConfiguration<MultiTenantSvoOwner>
    {
        public void Configure(EntityTypeBuilder<MultiTenantSvoOwner> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.PartyId).IsRequired();
        }
    }

    // ---- Auto-discovery variant (no explicit builder.Property call) --------
    // Models the case where an IMultiTenant entity exposes an SVO property
    // but no IEntityTypeConfiguration explicitly registers it as a scalar.
    // EF Core then auto-discovers the SVO type as a navigation entity, and
    // the multi-tenant filter loop's `Entity<TEntity>()` call re-fires that
    // discovery after ApplyGranitConventions has removed it.
    public sealed class MultiTenantAutoDiscoveryOwner : IMultiTenant
    {
        public int Id { get; set; }
        public Guid? TenantId { get; set; }
        public ReproPartyId? PartyId { get; set; }
    }

    // ---- String-backed SVO variant -----------------------------------------
    // Reuses the framework-shipped `BlobReference : SingleValueObject<string>`
    // so the test asserts that the ordering fix + SVO machinery are generic
    // over the primitive type, not Guid-specific.
    public sealed class MultiTenantBlobOwner : IMultiTenant
    {
        public int Id { get; set; }
        public Guid? TenantId { get; set; }
        public BlobReference BlobRef { get; set; } = null!;
    }

    private sealed class MultiTenantBlobOwnerConfiguration
        : IEntityTypeConfiguration<MultiTenantBlobOwner>
    {
        public void Configure(EntityTypeBuilder<MultiTenantBlobOwner> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.BlobRef).IsRequired();
        }
    }

    private sealed class OrderingReproDbContext(
        DbContextOptions<OrderingReproDbContext> options,
        ICurrentTenant tenant,
        IDataFilter? dataFilter = null)
        : GranitDbContext(options, tenant, dataFilter)
    {
        public DbSet<MultiTenantSvoOwner> Owners => Set<MultiTenantSvoOwner>();
        public DbSet<MultiTenantAutoDiscoveryOwner> AutoOwners => Set<MultiTenantAutoDiscoveryOwner>();
        public DbSet<MultiTenantBlobOwner> BlobOwners => Set<MultiTenantBlobOwner>();

        protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new MultiTenantSvoOwnerConfiguration());
            modelBuilder.ApplyConfiguration(new MultiTenantBlobOwnerConfiguration());
            // MultiTenantAutoDiscoveryOwner intentionally has no IEntityTypeConfiguration —
            // EF Core auto-discovery handles it, and `RemoveSingleValueObjectEntityTypes`
            // + `ApplySingleValueObjectConverters` must end up wrapping its SVO property.
        }
    }
}
