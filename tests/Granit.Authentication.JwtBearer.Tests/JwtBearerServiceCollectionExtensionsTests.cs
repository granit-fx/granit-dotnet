// =============================================================================
// Tests - JwtBearerServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitJwtBearer correctly registers:
//   - Generic JwtBearer authentication (section "Authentication")
//   - "Authenticated" authorization policy only
//   - CurrentUser and HttpContextAccessor services
// =============================================================================

using Granit.Authentication.JwtBearer.Authentication;
using Granit.Authentication.JwtBearer.Extensions;
using Granit.Authentication.JwtBearer.Options;
using Granit.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests;

public sealed class JwtBearerServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfiguration(
        string authority = "https://auth.test.com/realms/test",
        string audience = "test-client") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = authority,
                ["Authentication:Audience"] = audience,
                ["Authentication:RequireHttpsMetadata"] = "false"
            })
            .Build();

    [Fact]
    public void AddGranitJwtBearer_RegistersJwtBearerAuthOptions()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        JwtBearerAuthOptions options = sp.GetRequiredService<IOptions<JwtBearerAuthOptions>>().Value;
        options.Authority.ShouldBe("https://auth.test.com/realms/test");
        options.Audience.ShouldBe("test-client");
        options.RequireHttpsMetadata.ShouldBeFalse();
        options.NameClaimType.ShouldBe("sub");
    }

    [Fact]
    public void AddGranitJwtBearer_RegistersJwtBearerAuthentication()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Authority.ShouldBe("https://auth.test.com/realms/test");
        jwtOptions.Audience.ShouldBe("test-client");
        jwtOptions.RequireHttpsMetadata.ShouldBeFalse();
        jwtOptions.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        jwtOptions.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        jwtOptions.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        jwtOptions.TokenValidationParameters.NameClaimType.ShouldBe("sub");
    }

    [Fact]
    public void AddGranitJwtBearer_RegistersOnlyAuthenticatedPolicy()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — only "Authenticated" is registered in Granit.Authentication.JwtBearer (base)
        AuthorizationOptions authOptions = sp.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        authOptions.GetPolicy("Authenticated").ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitJwtBearer_RegistersCurrentUserService()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddGranitJwtBearer();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ICurrentUserService));

        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(CurrentUserService));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitJwtBearer_RegistersHttpContextAccessor()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = CreateConfiguration();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddGranitJwtBearer();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IHttpContextAccessor));

        descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitJwtBearer_WithCustomNameClaimType_UsesConfiguredNameClaimType()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://auth.test.com/realms/test",
                ["Authentication:Audience"] = "test-client",
                ["Authentication:NameClaimType"] = "email"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Act
        services.AddGranitJwtBearer();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        JwtBearerOptions jwtOptions = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.TokenValidationParameters.NameClaimType.ShouldBe("email");
    }
}
