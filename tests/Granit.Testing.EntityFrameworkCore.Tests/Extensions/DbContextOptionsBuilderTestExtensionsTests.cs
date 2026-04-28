using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Testing.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests.Extensions;

/// <summary>
/// Regression tests for <see cref="DbContextOptionsBuilderTestExtensions.EnableGranitTestModelIsolation{TContext}"/>.
/// Encodes the EF Core model-cache trap that bit
/// <c>Granit.Invoicing.EntityFrameworkCore.Tests.UnpaidInvoiceMetricsTests</c>: the
/// multi-tenant filter expression captures the first <see cref="ICurrentTenant"/>
/// instance EF sees, then reuses it for the rest of the process. The helper opts
/// the test out of EF's process-wide model cache so each <see cref="DbContextOptions"/>
/// captures its own tenant.
/// </summary>
public sealed class DbContextOptionsBuilderTestExtensionsTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        // Schema is shared across all DbContexts spun up in a test — create once via a
        // tenantless context so the IMultiTenant filter is not installed for setup.
        await using var schemaCtx = new MultiTenantTestDbContext(
            new DbContextOptionsBuilder<MultiTenantTestDbContext>()
                .UseSqlite(_connection)
                .EnableGranitTestModelIsolation()
                .Options,
            currentTenant: null);
        await schemaCtx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Two DbContexts built in the same test, with two different tenant mocks. Without
    /// <see cref="DbContextOptionsBuilderTestExtensions.EnableGranitTestModelIsolation{TContext}"/>,
    /// EF Core caches the first model and reuses its captured tenant for the second
    /// context's queries — the second tenant's filter would resolve to tenant A's id and
    /// hide tenant B's rows. With the helper, each options instance owns its model.
    /// </summary>
    [Fact]
    public async Task EnableGranitTestModelIsolation_LetsEachContextCaptureItsOwnTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        ICurrentTenant tenantContextA = Substitute.For<ICurrentTenant>();
        tenantContextA.IsAvailable.Returns(true);
        tenantContextA.Id.Returns(tenantA);

        ICurrentTenant tenantContextB = Substitute.For<ICurrentTenant>();
        tenantContextB.IsAvailable.Returns(true);
        tenantContextB.Id.Returns(tenantB);

        DbContextOptions<MultiTenantTestDbContext> optionsA =
            new DbContextOptionsBuilder<MultiTenantTestDbContext>()
                .UseSqlite(_connection)
                .EnableGranitTestModelIsolation()
                .Options;
        DbContextOptions<MultiTenantTestDbContext> optionsB =
            new DbContextOptionsBuilder<MultiTenantTestDbContext>()
                .UseSqlite(_connection)
                .EnableGranitTestModelIsolation()
                .Options;

        // Seed both tenants via a tenantless context (no IMultiTenant filter).
        await using (var seedCtx = new MultiTenantTestDbContext(optionsA, currentTenant: null))
        {
            seedCtx.Items.Add(new TenantItem(Guid.NewGuid(), tenantA, "A1"));
            seedCtx.Items.Add(new TenantItem(Guid.NewGuid(), tenantA, "A2"));
            seedCtx.Items.Add(new TenantItem(Guid.NewGuid(), tenantB, "B1"));
            await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var dbA = new MultiTenantTestDbContext(optionsA, tenantContextA);
        await using var dbB = new MultiTenantTestDbContext(optionsB, tenantContextB);

        IReadOnlyList<string> namesA = await dbA.Items
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        IReadOnlyList<string> namesB = await dbB.Items
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        namesA.ShouldBe(["A1", "A2"]);
        namesB.ShouldBe(["B1"]);
    }

    [Fact]
    public void EnableGranitTestModelIsolation_NullBuilder_Throws()
    {
        DbContextOptionsBuilder<MultiTenantTestDbContext>? builder = null;
        Should.Throw<ArgumentNullException>(() => builder!.EnableGranitTestModelIsolation());
    }

    private sealed class TenantItem : Entity, IMultiTenant
    {
        public Guid? TenantId { get; set; }
        public string Name { get; set; } = string.Empty;

        public TenantItem() { }

        public TenantItem(Guid id, Guid? tenantId, string name)
        {
            Id = id;
            TenantId = tenantId;
            Name = name;
        }
    }

    private sealed class MultiTenantTestDbContext(
        DbContextOptions<MultiTenantTestDbContext> options,
        ICurrentTenant? currentTenant) : DbContext(options)
    {
        private readonly ICurrentTenant? _currentTenant = currentTenant;

        public DbSet<TenantItem> Items => Set<TenantItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantItem>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.ApplyGranitConventions(_currentTenant, dataFilter: null);
        }
    }
}
