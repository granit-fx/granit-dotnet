using Granit.DataExchange.EntityFrameworkCore.Extensions;
using Granit.DataExchange.EntityFrameworkCore.Internal.Export;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Export;

public sealed class DbContextResolverTests
{
    [Fact]
    public void TryResolve_finds_registered_context_mapping_the_entity()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        DbContext? resolved = DbContextResolver.TryResolve(scope.ServiceProvider, typeof(TestEntity));

        resolved.ShouldBeOfType<TestAppDbContext>();
    }

    [Fact]
    public void TryResolve_returns_null_for_unmapped_entity()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        DbContext? resolved = DbContextResolver.TryResolve(scope.ServiceProvider, typeof(string));

        resolved.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_does_not_dispose_non_matching_scoped_contexts()
    {
        // The first registered context does not map TestEntity: the resolver inspects
        // and skips it. The instance is scope-owned — a later consumer in the same
        // scope must still be able to use it.
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        DbContext? resolved = DbContextResolver.TryResolve(scope.ServiceProvider, typeof(TestEntity));
        resolved.ShouldBeOfType<TestAppDbContext>();

        OtherDbContext other = scope.ServiceProvider.GetRequiredService<OtherDbContext>();
        Should.NotThrow(() => _ = other.Model);
    }

    [Fact]
    public void Resolve_throws_actionable_message_when_no_context_is_registered()
    {
        ServiceCollection services = new();
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => DbContextResolver.Resolve(provider, typeof(TestEntity)));

        ex.Message.ShouldContain("AddDataExchangeDbContext");
    }

    [Fact]
    public void AddDataExchangeDbContext_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddDataExchangeDbContext<TestAppDbContext>();
        services.AddDataExchangeDbContext<TestAppDbContext>();

        services.Count(d => d.ServiceType == typeof(DataExchangeDbContextRegistration)).ShouldBe(1);
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();

        // OtherDbContext registered FIRST so the resolver has to skip it.
        services.AddDbContext<OtherDbContext>(o => o.UseInMemoryDatabase("resolver-other"));
        services.AddDbContext<TestAppDbContext>(o => o.UseInMemoryDatabase("resolver-app"));
        services.AddDataExchangeDbContext<OtherDbContext>();
        services.AddDataExchangeDbContext<TestAppDbContext>();

        return services.BuildServiceProvider();
    }

    internal sealed class OtherEntity
    {
        public Guid Id { get; set; }
    }

    internal sealed class OtherDbContext(DbContextOptions<OtherDbContext> options) : DbContext(options)
    {
        public DbSet<OtherEntity> Others { get; set; } = null!;
    }
}
