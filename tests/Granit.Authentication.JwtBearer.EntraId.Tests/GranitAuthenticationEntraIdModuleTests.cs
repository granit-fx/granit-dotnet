// =============================================================================
// Tests - GranitAuthenticationEntraIdModule
// =============================================================================
// Verifies the complete DI wiring via ConfigureServices:
//   - ICurrentUserService resolvable (via dependency on GranitJwtBearerModule)
//   - EntraIdClaimsTransformation registered
// =============================================================================

using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.EntraId.Authentication;
using Granit.Modularity;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.EntraId.Tests;

public sealed class GranitAuthenticationEntraIdModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersICurrentUserService()
    {
        // Arrange
        GranitAuthenticationEntraIdModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraId:TenantId"] = "00000000-0000-0000-0000-000000000001";
        builder.Configuration["EntraId:ClientId"] = "test-client";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        // Call GranitJwtBearerModule first (dependency)
        new GranitJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ICurrentUserService? userService = sp.GetService<ICurrentUserService>();
        userService.ShouldNotBeNull("inherited from GranitJwtBearerModule");
    }

    [Fact]
    public void ConfigureServices_RegistersEntraIdClaimsTransformation()
    {
        // Arrange
        GranitAuthenticationEntraIdModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraId:TenantId"] = "00000000-0000-0000-0000-000000000001";
        builder.Configuration["EntraId:ClientId"] = "test-client";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitJwtBearerModule().ConfigureServices(context);

        // Act
        module.ConfigureServices(context);

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IClaimsTransformation))
            .ToList();

        descriptors.ShouldContain(d => d.ImplementationType == typeof(EntraIdClaimsTransformation));
    }

}
