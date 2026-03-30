// =============================================================================
// Tests - TenantPerSchemaDbContextFactory<TContext>
// =============================================================================
// Vérifie le guard tenant manquant et la construction correcte du DbContext.
// Aucune connexion PostgreSQL réelle n'est requise.
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
    : DbContext(options);

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

    private static TenantPerSchemaDbContextFactory<StubSchemaDbContext> BuildFactory(
        ICurrentTenant currentTenant)
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
            Configure = static builder => builder.UseInMemoryDatabase("schema_test"),
        };

        return new TenantPerSchemaDbContextFactory<StubSchemaDbContext>(
            currentTenant, schemaProvider, schemaActivator, sp, opts);
    }

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
}
