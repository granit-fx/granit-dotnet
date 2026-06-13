using Granit.Identity.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="UserSessionProviderServiceCollectionExtensions.SetUserSessionProvider{TProvider}"/>:
/// the explicit, order-independent precedence guard that decides which backend wins the
/// <see cref="IUserSessionProvider"/> / <see cref="IUserDeviceProvider"/> facets.
/// </summary>
public sealed class UserSessionProviderPrecedenceTests
{
    // Fake backends — resolvable with no external dependencies, so we can assert the resolved instance type.
    private sealed class LowProvider : IUserSessionProvider, IUserDeviceProvider
    {
        public Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(string u, string? c, CancellationToken t = default) => Task.FromResult<IReadOnlyList<UserSessionDescriptor>>([]);
        public Task<bool> RevokeAsync(string u, string s, CancellationToken t = default) => Task.FromResult(false);
        public Task<int> RevokeOthersAsync(string u, string c, CancellationToken t = default) => Task.FromResult(0);
        Task<IReadOnlyList<UserDevice>> IUserDeviceProvider.ListAsync(string u, CancellationToken t) => Task.FromResult<IReadOnlyList<UserDevice>>([]);
    }

    private sealed class HighProvider : IUserSessionProvider, IUserDeviceProvider
    {
        public Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(string u, string? c, CancellationToken t = default) => Task.FromResult<IReadOnlyList<UserSessionDescriptor>>([]);
        public Task<bool> RevokeAsync(string u, string s, CancellationToken t = default) => Task.FromResult(false);
        public Task<int> RevokeOthersAsync(string u, string c, CancellationToken t = default) => Task.FromResult(0);
        Task<IReadOnlyList<UserDevice>> IUserDeviceProvider.ListAsync(string u, CancellationToken t) => Task.FromResult<IReadOnlyList<UserDevice>>([]);
    }

    // Session-only backend (like BFF): does not implement IUserDeviceProvider.
    private sealed class SessionOnlyProvider : IUserSessionProvider
    {
        public Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(string u, string? c, CancellationToken t = default) => Task.FromResult<IReadOnlyList<UserSessionDescriptor>>([]);
        public Task<bool> RevokeAsync(string u, string s, CancellationToken t = default) => Task.FromResult(false);
        public Task<int> RevokeOthersAsync(string u, string c, CancellationToken t = default) => Task.FromResult(0);
    }

    private const UserSessionProviderPrecedence Low = UserSessionProviderPrecedence.Federated;
    private const UserSessionProviderPrecedence High = UserSessionProviderPrecedence.OpenIddict;

    [Theory]
    [InlineData(true)]  // low registered first
    [InlineData(false)] // high registered first
    public void Highest_precedence_wins_regardless_of_registration_order(bool lowFirst)
    {
        ServiceCollection services = [];

        if (lowFirst)
        {
            services.SetUserSessionProvider<LowProvider>(Low);
            services.SetUserSessionProvider<HighProvider>(High);
        }
        else
        {
            services.SetUserSessionProvider<HighProvider>(High);
            services.SetUserSessionProvider<LowProvider>(Low);
        }

        services.GetUserSessionProviderPrecedence().ShouldBe((High, High));

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUserSessionProvider>().ShouldBeOfType<HighProvider>();
        scope.ServiceProvider.GetRequiredService<IUserDeviceProvider>().ShouldBeOfType<HighProvider>();
    }

    [Fact]
    public void Session_only_backend_does_not_register_the_device_facet()
    {
        ServiceCollection services = [];
        // Device-capable backend first, then a higher session-only backend (the OpenIddict + BFF shape).
        services.SetUserSessionProvider<HighProvider>(High);
        services.SetUserSessionProvider<SessionOnlyProvider>(UserSessionProviderPrecedence.Bff);

        // Session goes to the higher session-only backend; device stays with the device-capable one.
        services.GetUserSessionProviderPrecedence()
            .ShouldBe((UserSessionProviderPrecedence.Bff, High));

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUserSessionProvider>().ShouldBeOfType<SessionOnlyProvider>();
        scope.ServiceProvider.GetRequiredService<IUserDeviceProvider>().ShouldBeOfType<HighProvider>();
    }

    [Fact]
    public void Both_facets_resolve_the_same_scoped_instance()
    {
        ServiceCollection services = [];
        services.SetUserSessionProvider<HighProvider>(High);

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        object session = scope.ServiceProvider.GetRequiredService<IUserSessionProvider>();
        object device = scope.ServiceProvider.GetRequiredService<IUserDeviceProvider>();

        session.ShouldBeSameAs(device);
    }

    [Fact]
    public void No_registration_reports_the_None_floor()
    {
        ServiceCollection services = [];
        services.GetUserSessionProviderPrecedence()
            .ShouldBe((UserSessionProviderPrecedence.None, UserSessionProviderPrecedence.None));
    }
}
