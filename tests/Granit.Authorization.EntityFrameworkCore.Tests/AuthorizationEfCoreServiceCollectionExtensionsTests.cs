using Granit.Authorization;
using Granit.Authorization.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class AuthorizationEfCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitAuthorizationEntityFrameworkCore_RegistersPermissionGrantStore()
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddGranitAuthorizationEntityFrameworkCore<TestDbContext>();

        services.ShouldContain(sd => sd.ServiceType == typeof(IPermissionGrantStore));
    }

    [Fact]
    public void AddGranitAuthorizationEntityFrameworkCore_ReturnsServices_ForChaining()
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        IServiceCollection returned = services.AddGranitAuthorizationEntityFrameworkCore<TestDbContext>();

        returned.ShouldBeSameAs(services);
    }
}
