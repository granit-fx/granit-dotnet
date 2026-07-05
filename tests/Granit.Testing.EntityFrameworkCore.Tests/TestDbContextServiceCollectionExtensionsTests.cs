using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Testing.EntityFrameworkCore.Extensions;
using Granit.Testing.Fakes;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests;

public sealed class TestDbContextServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTestDbContext_ThrowsOnNull()
    {
        IServiceCollection? services = null;

        Should.Throw<ArgumentNullException>(() => services!.AddGranitTestDbContext<TestDbContext>());
    }

    [Fact]
    public void AddGranitTestDbContext_Registers_Context()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddSingleton<ICurrentUserService>(new FakeCurrentUser());
        services.AddSingleton<IClock>(new FakeClock());
        services.AddSingleton<IGuidGenerator>(new FakeGuidGenerator());

        services.AddGranitTestDbContext<TestDbContext>();

        ServiceProvider provider = services.BuildServiceProvider();
        using TestDbContext context = provider.GetRequiredService<TestDbContext>();

        context.ShouldNotBeNull();
        context.Database.IsInMemory().ShouldBeTrue();
    }

    [Fact]
    public async Task AddGranitTestDbContext_Wires_Audit_Interceptor()
    {
        FakeClock clock = new();
        FakeCurrentUser user = new();

        ServiceCollection services = new();
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddSingleton<ICurrentUserService>(user);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IGuidGenerator>(new FakeGuidGenerator());
        services.AddGranitTestDbContext<TestDbContext>();

        ServiceProvider provider = services.BuildServiceProvider();
        await using TestDbContext context = provider.GetRequiredService<TestDbContext>();

        TestAuditedEntity entity = new() { Name = "ExtensionTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.CreatedAt.ShouldBe(clock.Now);
        entity.CreatedBy.ShouldBe(user.UserId);
    }

    [Fact]
    public void AddGranitTestDbContext_Returns_ServiceCollection()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddSingleton<ICurrentUserService>(new FakeCurrentUser());
        services.AddSingleton<IClock>(new FakeClock());
        services.AddSingleton<IGuidGenerator>(new FakeGuidGenerator());

        IServiceCollection result = services.AddGranitTestDbContext<TestDbContext>();

        result.ShouldBeSameAs(services);
    }
}
