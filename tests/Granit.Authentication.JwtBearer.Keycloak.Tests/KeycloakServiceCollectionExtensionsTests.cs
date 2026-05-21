// =============================================================================
// Tests - KeycloakServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitKeycloak correctly registers:
//   - KeycloakOptions from the "Keycloak" section
//   - Configure JWT Bearer (Authority, Audience, NameClaimType) so the
//     framework's PostConfigure can build ConfigurationManager
//   - KeycloakClaimsTransformation
// =============================================================================

using Granit.Authentication.JwtBearer.Extensions;
using Granit.Authentication.JwtBearer.Keycloak.Authentication;
using Granit.Authentication.JwtBearer.Keycloak.Extensions;
using Granit.Authentication.JwtBearer.Keycloak.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Keycloak.Tests;

public sealed class KeycloakServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfiguration(
        string authority = "https://keycloak.test/realms/test",
        string clientId = "test-client") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Keycloak:Authority"] = authority,
                ["Authentication:Keycloak:ClientId"] = clientId,
                ["Authentication:Keycloak:RequireHttpsMetadata"] = "false",
            })
            .Build();

    [Fact]
    public void AddGranitKeycloak_RegistersKeycloakOptions()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        KeycloakOptions options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        options.Authority.ShouldBe("https://keycloak.test/realms/test");
        options.ClientId.ShouldBe("test-client");
        options.RequireHttpsMetadata.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitKeycloak_PostConfiguresJwtBearer_WithKeycloakValues()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — PostConfigure overrides the JWT Bearer values
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Authority.ShouldBe("https://keycloak.test/realms/test");
        jwtOptions.Audience.ShouldBe("test-client", "ClientId is used as Audience by default");
        jwtOptions.TokenValidationParameters.NameClaimType.ShouldBe("preferred_username");
    }

    [Fact]
    public void AddGranitKeycloak_WithCustomAudience_UsesAudienceOverClientId()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Keycloak:Authority"] = "https://keycloak.test/realms/test",
                ["Authentication:Keycloak:ClientId"] = "test-client",
                ["Authentication:Keycloak:Audience"] = "custom-audience",
                ["Authentication:Keycloak:RequireHttpsMetadata"] = "false"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Audience.ShouldBe("custom-audience");
    }

    [Fact]
    public void AddGranitKeycloak_OnlyKeycloakSection_MaterialisesConfigurationManager()
    {
        // Regression: a consumer that only provides the "Keycloak" section
        // (no "Authentication" section) must still get a fully-wired
        // JwtBearerOptions with a non-null ConfigurationManager. The framework's
        // built-in JwtBearerPostConfigureOptions is what materialises that
        // manager from Authority, and it runs in the PostConfigure phase — so
        // Granit's Keycloak overrides MUST run in the Configure phase (not
        // PostConfigure), otherwise Authority is still empty when the framework
        // looks at it, no manager is created, JWKS is never fetched, and every
        // inbound token fails with IDX10500.
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // No "Authentication:*" keys — only Keycloak.
                ["Authentication:Keycloak:Authority"] = "https://keycloak.test/realms/test",
                ["Authentication:Keycloak:ClientId"] = "test-client",
                ["Authentication:Keycloak:RequireHttpsMetadata"] = "false",
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();
        services.AddGranitKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Authority.ShouldBe("https://keycloak.test/realms/test");
        jwtOptions.ConfigurationManager.ShouldNotBeNull(
            "Framework JwtBearerPostConfigureOptions must see Authority set when it runs, " +
            "otherwise the OpenIdConnectConfiguration manager is never created and JWKS is " +
            "never fetched (IDX10500 on every inbound token).");
    }

    [Fact]
    public void AddGranitKeycloak_RegistersClaimsTransformation()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitKeycloak();

        // Assert
        var descriptors = services
            .Where(d => d.ServiceType == typeof(IClaimsTransformation))
            .ToList();

        descriptors.ShouldContain(d => d.ImplementationType == typeof(KeycloakClaimsTransformation));
    }

}
