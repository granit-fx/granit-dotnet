// =============================================================================
// Tests - EntraIdServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitEntraId correctly registers:
//   - EntraIdOptions from the "EntraId" section
//   - PostConfigure JWT Bearer (Authority, Audience, NameClaimType)
//   - EntraIdClaimsTransformation
// =============================================================================

using Granit.Authentication.JwtBearer.EntraId.Authentication;
using Granit.Authentication.JwtBearer.EntraId.Extensions;
using Granit.Authentication.JwtBearer.EntraId.Options;
using Granit.Authentication.JwtBearer.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.EntraId.Tests;

public sealed class EntraIdServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfiguration(
        string tenantId = "00000000-0000-0000-0000-000000000001",
        string clientId = "test-client") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:TenantId"] = tenantId,
                ["EntraId:ClientId"] = clientId,
                ["EntraId:Instance"] = "https://login.microsoftonline.com/",
                ["EntraId:RequireHttpsMetadata"] = "false"
            })
            .Build();

    [Fact]
    public void AddGranitEntraId_RegistersEntraIdOptions()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitEntraId();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        EntraIdOptions options = sp.GetRequiredService<IOptions<EntraIdOptions>>().Value;
        options.TenantId.ShouldBe("00000000-0000-0000-0000-000000000001");
        options.ClientId.ShouldBe("test-client");
        options.RequireHttpsMetadata.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitEntraId_PostConfiguresJwtBearer_WithEntraIdValues()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitEntraId();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — PostConfigure overrides JWT Bearer values with Entra ID
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Authority.ShouldBe("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000001/v2.0");
        jwtOptions.Audience.ShouldBe("test-client");
        jwtOptions.TokenValidationParameters.NameClaimType.ShouldBe("preferred_username");
    }

    [Fact]
    public void AddGranitEntraId_RegistersClaimsTransformation()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);
        services.AddGranitJwtBearer();

        // Act
        services.AddGranitEntraId();

        // Assert
        var descriptors = services
            .Where(d => d.ServiceType == typeof(IClaimsTransformation))
            .ToList();

        descriptors.ShouldContain(d => d.ImplementationType == typeof(EntraIdClaimsTransformation));
    }

}
