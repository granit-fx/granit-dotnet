// =============================================================================
// Tests - KeycloakServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitKeycloak correctly registers:
//   - KeycloakOptions from the "Keycloak" section
//   - PostConfigure JWT Bearer (Authority, Audience, NameClaimType)
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
                ["Keycloak:Authority"] = authority,
                ["Keycloak:ClientId"] = clientId,
                ["Keycloak:RequireHttpsMetadata"] = "false",
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
                ["Keycloak:Authority"] = "https://keycloak.test/realms/test",
                ["Keycloak:ClientId"] = "test-client",
                ["Keycloak:Audience"] = "custom-audience",
                ["Keycloak:RequireHttpsMetadata"] = "false"
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
