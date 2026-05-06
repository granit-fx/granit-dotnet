using Granit.Authentication.ApiKeys.Extensions;
using Granit.Authentication.ApiKeys.Internal;
using Granit.Authentication.ApiKeys.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitApiKeyAuthentication_RegistersApiKeyGenerator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddAuthentication();

        services.AddGranitApiKeyAuthentication();

        using ServiceProvider provider = services.BuildServiceProvider();
        IApiKeyGenerator? generator = provider.GetService<IApiKeyGenerator>();

        generator.ShouldNotBeNull();
        generator.ShouldBeOfType<ApiKeyGenerator>();
    }

    [Fact]
    public async Task AddGranitApiKeyAuthentication_RegistersAuthenticationScheme()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddAuthentication();

        services.AddGranitApiKeyAuthentication();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IAuthenticationSchemeProvider schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        AuthenticationScheme? scheme = await schemeProvider.GetSchemeAsync(ApiKeyAuthenticationDefaults.AuthenticationScheme);

        scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe(ApiKeyAuthenticationDefaults.AuthenticationScheme);
    }

    [Fact]
    public void AddGranitApiKeyAuthentication_AppliesCustomOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();

        services.AddGranitApiKeyAuthentication(options =>
        {
            options.CacheDuration = TimeSpan.FromMinutes(30);
            options.TrackLastUsed = false;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<ApiKeyOptions> optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ApiKeyOptions>>();
        ApiKeyOptions options = optionsMonitor.Get(ApiKeyAuthenticationDefaults.AuthenticationScheme);

        options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(30));
        options.TrackLastUsed.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitApiKeyAuthentication_NullOptions_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();

        services.AddGranitApiKeyAuthentication(configureOptions: null);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<ApiKeyOptions> optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ApiKeyOptions>>();
        ApiKeyOptions options = optionsMonitor.Get(ApiKeyAuthenticationDefaults.AuthenticationScheme);

        options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
        options.TrackLastUsed.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitApiKeyAuthentication_ThrowsOnNullServices() =>
        Should.Throw<ArgumentNullException>(() =>
            ApiKeyServiceCollectionExtensions.AddGranitApiKeyAuthentication(null!));

    [Fact]
    public void AddGranitApiKeyAuthentication_DoesNotDuplicateGenerator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();

        // Call twice
        services.AddGranitApiKeyAuthentication();
        services.AddGranitApiKeyAuthentication();

        var generatorDescriptors = services
            .Where(d => d.ServiceType == typeof(IApiKeyGenerator))
            .ToList();

        // TryAddSingleton ensures only one registration
        generatorDescriptors.Count.ShouldBe(1);
    }

    [Fact]
    public void AddGranitApiKeyAuthentication_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();

        IServiceCollection returned = services.AddGranitApiKeyAuthentication();

        returned.ShouldBeSameAs(services);
    }
}
