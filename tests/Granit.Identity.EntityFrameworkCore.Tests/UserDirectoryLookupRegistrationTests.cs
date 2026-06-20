using Granit.DataLookup.Sources;
using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class UserDirectoryLookupRegistrationTests
{
    private sealed class StubDirectory : IUserDirectoryQueryableSource
    {
        public IQueryable<User> GetQueryable() => Enumerable.Empty<User>().AsQueryable();
    }

    [Fact]
    public void AddUserDirectoryLookup_registers_a_users_lookup_source_gated_on_users_read()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserDirectoryQueryableSource>(new StubDirectory());

        services.AddUserDirectoryLookup();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        ILookupSource? source = scope.ServiceProvider.GetServices<ILookupSource>()
            .SingleOrDefault(s => s.Name == "users");

        source.ShouldNotBeNull();
        source.RequiredPermission.ShouldBe("Identity.Users.Read");
    }

    [Fact]
    public void AddUserDirectoryLookup_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserDirectoryQueryableSource>(new StubDirectory());

        services.AddUserDirectoryLookup();
        services.AddUserDirectoryLookup();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetServices<ILookupSource>()
            .Count(s => s.Name == "users")
            .ShouldBe(1);
    }
}
