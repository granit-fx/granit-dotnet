// =============================================================================
// Tests - TenantPerSchemaDbContextFactory<TContext>
// =============================================================================
// Verifies the missing-tenant guard, DbContext construction, and that opening
// a real (SQLite) connection triggers schema activation with the resolved
// schema name via TenantSchemaConnectionInterceptor — the behavior the former
// InMemory setup could not exercise (no connection ever opened).
// =============================================================================

using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

internal sealed class StubSchemaDbContext(DbContextOptions<StubSchemaDbContext> options)
    : DbContext(options)
{
    public DbSet<StubSchemaRow> Rows => Set<StubSchemaRow>();
}

internal sealed class StubSchemaRow
{
    public Guid Id { get; set; }
}

public sealed class TenantPerSchemaDbContextFactoryTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    private static ICurrentTenant MakeTenant(Guid? id)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(id);
        tenant.IsAvailable.Returns(id.HasValue);
        return tenant;
    }

    private static (TenantPerSchemaDbContextFactory<StubSchemaDbContext> factory,
                    ITenantSchemaActivator activator)
        BuildFactoryWithActivator(ICurrentTenant currentTenant)
    {
        ITenantSchemaProvider schemaProvider = Substitute.For<ITenantSchemaProvider>();
#pragma warning disable CA2012 // NSubstitute setup pattern — ValueTask not consumed directly
        schemaProvider.GetSchemaNameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult("tenant_stub"));
#pragma warning restore CA2012

        ITenantSchemaActivator schemaActivator = Substitute.For<ITenantSchemaActivator>();

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TenantPerSchemaDbContextOptions<StubSchemaDbContext> opts = new()
        {
            Configure = static builder => builder.UseSqlite("Data Source=:memory:"),
        };

        return (new TenantPerSchemaDbContextFactory<StubSchemaDbContext>(
            currentTenant, schemaProvider, schemaActivator, sp, opts), schemaActivator);
    }

    private static TenantPerSchemaDbContextFactory<StubSchemaDbContext> BuildFactory(
        ICurrentTenant currentTenant) => BuildFactoryWithActivator(currentTenant).factory;

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_WhenTenantActive_ReturnsDbContext()
    {
        TenantPerSchemaDbContextFactory<StubSchemaDbContext> factory =
            BuildFactory(MakeTenant(TenantA));

        await using StubSchemaDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        ctx.ShouldNotBeNull();
    }

    [Fact]
    public void CreateDbContext_WhenTenantActive_ReturnsDbContext()
    {
        TenantPerSchemaDbContextFactory<StubSchemaDbContext> factory =
            BuildFactory(MakeTenant(TenantA));

        using StubSchemaDbContext ctx = factory.CreateDbContext();

        ctx.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // Guard — pas de tenant → exception (ISO 27001 : pas de fallback silencieux)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_WhenNoTenantActive_ThrowsInvalidOperationException()
    {
        TenantPerSchemaDbContextFactory<StubSchemaDbContext> factory =
            BuildFactory(MakeTenant(null));

        Func<Task> act = async () =>
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("No active tenant context");
    }

    [Fact]
    public void CreateDbContext_WhenNoTenantActive_ThrowsInvalidOperationException()
    {
        TenantPerSchemaDbContextFactory<StubSchemaDbContext> factory =
            BuildFactory(MakeTenant(null));

        Action act = () => factory.CreateDbContext();

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("No active tenant context");
    }

    // -----------------------------------------------------------------------
    // Schema activation — opening a real connection activates the tenant schema
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpeningConnection_ActivatesResolvedTenantSchema()
    {
        (TenantPerSchemaDbContextFactory<StubSchemaDbContext> factory,
         ITenantSchemaActivator activator) = BuildFactoryWithActivator(MakeTenant(TenantA));

        await using StubSchemaDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await ctx.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        await activator.Received(1).ActivateSchemaAsync(
            Arg.Any<System.Data.Common.DbConnection>(),
            "tenant_stub",
            Arg.Any<CancellationToken>());
    }
}
