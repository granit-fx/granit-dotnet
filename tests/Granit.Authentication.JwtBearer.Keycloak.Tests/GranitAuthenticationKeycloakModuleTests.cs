// =============================================================================
// Tests - GranitJwtBearerKeycloakModule
// =============================================================================
// Verifies the complete DI wiring via ConfigureServices:
//   - ICurrentUserService resolvable (via dependency on GranitJwtBearerModule)
//   - KeycloakClaimsTransformation registered
//   - "Admin" policy registered
// =============================================================================

using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.Keycloak.Authentication;
using Granit.Modularity;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Keycloak.Tests;

public sealed class GranitJwtBearerKeycloakModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersICurrentUserService()
    {
        // Arrange
        GranitJwtBearerKeycloakModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Keycloak:Authority"] = "https://keycloak.test/realms/test";
        builder.Configuration["Keycloak:ClientId"] = "test-client";
        // Call GranitJwtBearerModule first (dependency)
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ICurrentUserService? userService = sp.GetService<ICurrentUserService>();
        userService.ShouldNotBeNull("inherited from GranitJwtBearerModule");
    }

    [Fact]
    public void ConfigureServices_RegistersKeycloakClaimsTransformation()
    {
        // Arrange
        GranitJwtBearerKeycloakModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Keycloak:Authority"] = "https://keycloak.test/realms/test";
        builder.Configuration["Keycloak:ClientId"] = "test-client";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IClaimsTransformation))
            .ToList();

        descriptors.ShouldContain(d => d.ImplementationType == typeof(KeycloakClaimsTransformation));
    }

    [Fact]
    public void ConfigureServices_RegistersAdminPolicy()
    {
        // Arrange
        GranitJwtBearerKeycloakModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Keycloak:Authority"] = "https://keycloak.test/realms/test";
        builder.Configuration["Keycloak:ClientId"] = "test-client";
        builder.Configuration["Keycloak:AdminRole"] = "admin";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        AuthorizationOptions authOptions = sp.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        authOptions.GetPolicy("Admin").ShouldNotBeNull();
    }
}
