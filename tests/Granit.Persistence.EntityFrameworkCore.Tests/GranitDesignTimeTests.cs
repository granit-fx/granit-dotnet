using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

// Pins the design-time-vs-runtime parity that motivated GranitDesignTime + NullDataFilter:
// without these stubs, an IDesignTimeDbContextFactory passing (null, null) produces a
// snapshot whose IMultiTenant query filter is missing, and EF Core 10's
// PendingModelChangesWarning then fires at app startup.
public sealed class GranitDesignTimeTests
{
    [Fact]
    public void NullServices_DoNotRegisterMultiTenantFilter()
    {
        DbContextOptions<NullStubsParityDbContext> options =
            new DbContextOptionsBuilder<NullStubsParityDbContext>()
                .UseInMemoryDatabase(databaseName: nameof(NullStubsParityDbContext))
                .Options;
        using var context = new NullStubsParityDbContext(options);

        IEntityType entityType = context.Model.FindEntityType(typeof(ParityTenantEntity))!;

        entityType.GetDeclaredQueryFilters()
            .Any(f => f.Key == GranitFilterNames.MultiTenant)
            .ShouldBeFalse("plain DbContexts never get the tenant filter — GranitDbContext registers it");
    }

    [Fact]
    public void DesignTimeStubs_RegisterMultiTenantFilter_LikeRuntime()
    {
        DbContextOptions<DesignTimeStubsParityDbContext> options =
            new DbContextOptionsBuilder<DesignTimeStubsParityDbContext>()
                .UseInMemoryDatabase(databaseName: nameof(DesignTimeStubsParityDbContext))
                .Options;
        using var context = new DesignTimeStubsParityDbContext(options);

        IEntityType entityType = context.Model.FindEntityType(typeof(ParityTenantEntity))!;

        entityType.GetDeclaredQueryFilters()
            .Any(f => f.Key == GranitFilterNames.MultiTenant)
            .ShouldBeTrue(
                "passing the framework-provided stubs must produce the same model snapshot as runtime DI");
    }

    [Fact]
    public void DesignTimeStubs_AreSingletonsAndStateless()
    {
        GranitDesignTime.CurrentTenant.ShouldBeSameAs(NullTenantContext.Instance);
        GranitDesignTime.DataFilter.ShouldBeSameAs(NullDataFilter.Instance);
        GranitDesignTime.CurrentTenant.IsAvailable.ShouldBeFalse();
        GranitDesignTime.DataFilter.IsEnabled<IMultiTenant>().ShouldBeTrue();
    }
}

internal sealed class ParityTenantEntity : Entity, IMultiTenant
{
    public Guid? TenantId { get; set; }
}

// Distinct DbContext types so EF Core's model cache (keyed by DbContext CLR type)
// stores an independent model per parity scenario.
internal sealed class NullStubsParityDbContext(DbContextOptions<NullStubsParityDbContext> options)
    : DbContext(options)
{
    public DbSet<ParityTenantEntity> Tenants => Set<ParityTenantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

// The design-time factory pattern since #3162: a GranitDbContext derivative constructed with
// the framework stubs — the tenant filter comes from the base class, never from a legacy call.
internal sealed class DesignTimeStubsParityDbContext(DbContextOptions<DesignTimeStubsParityDbContext> options)
    : GranitDbContext(options, GranitDesignTime.CurrentTenant, GranitDesignTime.DataFilter)
{
    public DbSet<ParityTenantEntity> Tenants => Set<ParityTenantEntity>();
}
