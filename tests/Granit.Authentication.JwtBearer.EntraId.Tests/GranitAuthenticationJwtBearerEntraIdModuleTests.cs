// =============================================================================
// Tests - GranitAuthenticationJwtBearerEntraIdModule
// =============================================================================
// Verifies the complete DI wiring via ConfigureServices:
//   - ICurrentUserService resolvable (via dependency on GranitAuthenticationJwtBearerModule)
//   - EntraIdClaimsTransformation registered
// =============================================================================

using Granit.Authentication.JwtBearer.EntraId.Authentication;
using Granit.Modularity;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.EntraId.Tests;

public sealed class GranitAuthenticationJwtBearerEntraIdModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersICurrentUserService()
    {
        // Arrange
        GranitAuthenticationJwtBearerEntraIdModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Authentication:EntraId:TenantId"] = "00000000-0000-0000-0000-000000000001";
        builder.Configuration["Authentication:EntraId:ClientId"] = "test-client";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        // Call GranitAuthenticationJwtBearerModule first (dependency)
        new GranitAuthenticationJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ICurrentUserService? userService = sp.GetService<ICurrentUserService>();
        userService.ShouldNotBeNull("inherited from GranitAuthenticationJwtBearerModule");
    }

    [Fact]
    public void ConfigureServices_RegistersEntraIdClaimsTransformation()
    {
        // Arrange
        GranitAuthenticationJwtBearerEntraIdModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Authentication:EntraId:TenantId"] = "00000000-0000-0000-0000-000000000001";
        builder.Configuration["Authentication:EntraId:ClientId"] = "test-client";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitAuthenticationJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IClaimsTransformation))
            .ToList();

        descriptors.ShouldContain(d => d.ImplementationType == typeof(EntraIdClaimsTransformation));
    }

}
