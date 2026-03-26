using Granit.Authentication.JwtBearer.BackChannelLogout;
using Granit.Authentication.JwtBearer.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authentication.JwtBearer.Tests.BackChannelLogout;

public sealed class BackChannelLogoutRegistrationTests
{
    private static IConfiguration CreateConfiguration(bool backChannelEnabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://idp.test/realms/test",
                ["Authentication:Audience"] = "test-client",
                ["Authentication:RequireHttpsMetadata"] = "false",
                ["Authentication:BackChannelLogout:Enabled"] = backChannelEnabled.ToString(),
                ["Authentication:BackChannelLogout:EndpointPath"] = "/auth/back-channel-logout",
                ["Authentication:BackChannelLogout:SessionRevocationTtl"] = "01:00:00",
            })
            .Build();

    [Fact]
    public void AddGranitJwtBearer_BackChannelEnabled_RegistersRevokedSessionStore()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateConfiguration(backChannelEnabled: true));
        services.AddLogging();
        services.AddSingleton<IFusionCache>(_ => new FusionCache(new FusionCacheOptions()));

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        IRevokedSessionStore? store = sp.GetService<IRevokedSessionStore>();
        store.ShouldNotBeNull();
        store.ShouldBeOfType<DistributedCacheRevokedSessionStore>();
    }

    [Fact]
    public void AddGranitJwtBearer_BackChannelEnabled_WiresOnTokenValidated()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateConfiguration(backChannelEnabled: true));
        services.AddLogging();
        services.AddSingleton<IFusionCache>(_ => new FusionCache(new FusionCacheOptions()));

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Events.ShouldNotBeNull();
        jwtOptions.Events.OnTokenValidated.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitJwtBearer_BackChannelEnabled_OnTokenValidatedDiffersFromDefault()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateConfiguration(backChannelEnabled: true));
        services.AddLogging();
        services.AddSingleton<IFusionCache>(_ => new FusionCache(new FusionCacheOptions()));

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — When enabled, OnTokenValidated should be our custom delegate,
        // distinct from the framework default.
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        JwtBearerEvents defaultEvents = new();
        jwtOptions.Events.ShouldNotBeNull();
        jwtOptions.Events.OnTokenValidated.ShouldNotBe(defaultEvents.OnTokenValidated);
    }

    [Fact]
    public void AddGranitJwtBearer_AlwaysRegistersStore_EvenWhenDisabled()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateConfiguration(backChannelEnabled: false));
        services.AddLogging();
        services.AddSingleton<IFusionCache>(_ => new FusionCache(new FusionCacheOptions()));

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        sp.GetService<IRevokedSessionStore>().ShouldNotBeNull();
    }
}
