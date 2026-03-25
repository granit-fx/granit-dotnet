using Granit.Authentication.JwtBearer.Cognito.Authentication;
using Granit.Authentication.JwtBearer.Cognito.Extensions;
using Granit.Authentication.JwtBearer.Cognito.Options;
using Granit.Authentication.JwtBearer.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Cognito.Tests;

public sealed class CognitoServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfiguration(
        string authority = "https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_ABC123",
        string clientId = "test-client")
    {
        Dictionary<string, string?> config = new()
        {
            ["Authentication:Authority"] = authority,
            ["Authentication:Audience"] = clientId,
            ["Cognito:Authority"] = authority,
            ["Cognito:ClientId"] = clientId,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    [Fact]
    public void AddGranitCognito_RegistersCognitoOptions()
    {
        IConfiguration config = CreateConfiguration();
        ServiceCollection services = new();
        services.AddSingleton(config);
        services.AddGranitJwtBearer();
        services.AddGranitCognito();
        ServiceProvider provider = services.BuildServiceProvider();

        CognitoOptions options = provider.GetRequiredService<IOptions<CognitoOptions>>().Value;

        options.ClientId.ShouldBe("test-client");
    }

    [Fact]
    public void AddGranitCognito_OverridesJwtBearerAuthority()
    {
        IConfiguration config = CreateConfiguration(authority: "https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_XYZ");
        ServiceCollection services = new();
        services.AddSingleton(config);
        services.AddGranitJwtBearer();
        services.AddGranitCognito();
        ServiceProvider provider = services.BuildServiceProvider();

        JwtBearerOptions jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Authority.ShouldBe("https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_XYZ");
    }

    [Fact]
    public void AddGranitCognito_SetsNameClaimTypeToCognitoUsername()
    {
        IConfiguration config = CreateConfiguration();
        ServiceCollection services = new();
        services.AddSingleton(config);
        services.AddGranitJwtBearer();
        services.AddGranitCognito();
        ServiceProvider provider = services.BuildServiceProvider();

        JwtBearerOptions jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.TokenValidationParameters.NameClaimType.ShouldBe("cognito:username");
    }

    [Fact]
    public void AddGranitCognito_RegistersClaimsTransformation()
    {
        IConfiguration config = CreateConfiguration();
        ServiceCollection services = new();
        services.AddSingleton(config);
        services.AddGranitJwtBearer();
        services.AddGranitCognito();
        ServiceProvider provider = services.BuildServiceProvider();

        IClaimsTransformation transformation = provider.GetRequiredService<IClaimsTransformation>();

        transformation.ShouldBeOfType<CognitoClaimsTransformation>();
    }

    [Fact]
    public void AddGranitCognito_UsesClientIdAsDefaultAudience()
    {
        Dictionary<string, string?> configData = new()
        {
            ["Authentication:Authority"] = "https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_ABC",
            ["Authentication:Audience"] = "fallback",
            ["Cognito:Authority"] = "https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_ABC",
            ["Cognito:ClientId"] = "my-app-client",
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(config);
        services.AddGranitJwtBearer();
        services.AddGranitCognito();
        ServiceProvider provider = services.BuildServiceProvider();

        JwtBearerOptions jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Audience.ShouldBe("my-app-client", "ClientId is used as Audience by default");
    }
}
