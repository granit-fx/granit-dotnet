// =============================================================================
// Tests - GranitAuthenticationJwtBearerKeycloakModule
// =============================================================================
// Verifies the complete DI wiring via ConfigureServices:
//   - ICurrentUserService resolvable (via dependency on GranitAuthenticationJwtBearerModule)
//   - KeycloakClaimsTransformation registered
// =============================================================================

using Granit.Authentication.JwtBearer.Keycloak.Authentication;
using Granit.Modularity;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Keycloak.Tests;

public sealed class GranitAuthenticationJwtBearerKeycloakModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersICurrentUserService()
    {
        // Arrange
        GranitAuthenticationJwtBearerKeycloakModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Keycloak:Authority"] = "https://keycloak.test/realms/test";
        builder.Configuration["Keycloak:ClientId"] = "test-client";
        // Call GranitAuthenticationJwtBearerModule first (dependency)
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitAuthenticationJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ICurrentUserService? userService = sp.GetService<ICurrentUserService>();
        userService.ShouldNotBeNull("inherited from GranitAuthenticationJwtBearerModule");
    }

    [Fact]
    public void ConfigureServices_RegistersKeycloakClaimsTransformation()
    {
        // Arrange
        GranitAuthenticationJwtBearerKeycloakModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Keycloak:Authority"] = "https://keycloak.test/realms/test";
        builder.Configuration["Keycloak:ClientId"] = "test-client";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitAuthenticationJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IClaimsTransformation))
            .ToList();

        descriptors.ShouldContain(d => d.ImplementationType == typeof(KeycloakClaimsTransformation));
    }

}
