using Granit.Authentication.JwtBearer.Cognito.Authentication;
using Granit.Authentication.JwtBearer.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Cognito.Tests;

public sealed class GranitAuthenticationCognitoModuleTests
{
    private static ServiceProvider BuildProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_TEST",
            ["Authentication:Audience"] = "test",
            ["Cognito:Authority"] = "https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_TEST",
            ["Cognito:ClientId"] = "test-client",
            ["Cognito:AdminGroup"] = "admins",
        });

        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddGranitJwtBearer();

        GranitAuthenticationCognitoModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitAuthenticationCognitoModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void ConfigureServices_RegistersClaimsTransformation()
    {
        ServiceProvider provider = BuildProvider();

        IClaimsTransformation transformation = provider.GetRequiredService<IClaimsTransformation>();

        transformation.ShouldBeOfType<CognitoClaimsTransformation>();
    }

    [Fact]
    public void ConfigureServices_RegistersAdminPolicy()
    {
        ServiceProvider provider = BuildProvider();

        AuthorizationOptions authOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        authOptions.GetPolicy("Admin").ShouldNotBeNull();
    }
}
