using Granit.Bff.UserSessions;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Modularity;
using Granit.OpenIddict.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests;

/// <summary>
/// End-to-end registration tests for the canonical session/device providers across the real backends
/// (no-op abstractions defaults, OpenIddict, BFF). Locks in the fix for the "<c>/sessions</c> returns
/// <c>200 []</c>" bug: the OpenIddict provider is now co-located with <c>AddGranitOpenIddict</c> (not the
/// module), and precedence between coexisting backends is explicit and order-independent.
/// </summary>
public sealed class UserSessionProviderRegistrationTests
{
    private static IServiceCollection NewServices()
    {
        HostApplicationBuilder builder = new();
        return builder.Services;
    }

    private static void RunModule(IServiceCollection services, GranitModule module)
    {
        HostApplicationBuilder builder = new();
        // Reuse the same collection so several modules accumulate into one graph.
        ServiceConfigurationContext context = new(services, builder.Configuration, builder);
        module.ConfigureServices(context);
    }

    [Fact]
    public void Abstractions_alone_resolves_the_fallback_provider()
    {
        // The reported bug: only the always-present abstractions module ran (no backend in the graph),
        // so the canonical endpoints resolve the no-op provider and serve 200 [].
        IServiceCollection services = NewServices();
        RunModule(services, new GranitIdentityAbstractionsModule());

        services.GetUserSessionProviderPrecedence()
            .ShouldBe((UserSessionProviderPrecedence.None, UserSessionProviderPrecedence.None));

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        // The fallback marker is what the startup warning keys on.
        scope.ServiceProvider.GetRequiredService<IUserSessionProvider>()
            .ShouldBeAssignableTo<IFallbackUserSessionProvider>();
    }

    [Fact]
    public void OpenIddict_module_no_longer_registers_a_session_provider()
    {
        // Regression guard for the co-location: the Replace moved OUT of GranitOpenIddictModule into
        // AddGranitOpenIddict. Running the module alone must NOT wire a provider.
        IServiceCollection services = NewServices();
        RunModule(services, new GranitOpenIddictModule());

        services.GetUserSessionProviderPrecedence()
            .ShouldBe((UserSessionProviderPrecedence.None, UserSessionProviderPrecedence.None));
        services.Any(d => d.ServiceType == typeof(IUserSessionProvider)).ShouldBeFalse();
    }

    [Fact]
    public void AddOpenIddictUserSessionProvider_wins_over_the_fallback()
    {
        // What AddGranitOpenIddict now calls. This is the seam that fixes the bug for the common path.
        IServiceCollection services = NewServices();
        RunModule(services, new GranitIdentityAbstractionsModule());
        services.AddOpenIddictUserSessionProvider();

        services.GetUserSessionProviderPrecedence()
            .ShouldBe((UserSessionProviderPrecedence.OpenIddict, UserSessionProviderPrecedence.OpenIddict));
    }

    [Theory]
    [InlineData(true)]  // OpenIddict registered first
    [InlineData(false)] // BFF registered first
    public void OpenIddict_and_Bff_together_Bff_wins_session_OpenIddict_wins_device_either_order(bool openIddictFirst)
    {
        // The contested case (OpenIddict authority + BFF gateway on one host). The winner must be
        // deterministic, NOT an accident of which one registered last.
        IServiceCollection services = NewServices();
        RunModule(services, new GranitIdentityAbstractionsModule());

        if (openIddictFirst)
        {
            services.AddOpenIddictUserSessionProvider();
            RunModule(services, new GranitBffUserSessionsModule());
        }
        else
        {
            RunModule(services, new GranitBffUserSessionsModule());
            services.AddOpenIddictUserSessionProvider();
        }

        services.GetUserSessionProviderPrecedence()
            .ShouldBe((UserSessionProviderPrecedence.Bff, UserSessionProviderPrecedence.OpenIddict));
    }
}
