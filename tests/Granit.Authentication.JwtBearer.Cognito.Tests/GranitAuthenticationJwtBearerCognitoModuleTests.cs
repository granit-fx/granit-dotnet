using Granit.Authentication.JwtBearer.Cognito.Authentication;
using Granit.Authentication.JwtBearer.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Cognito.Tests;

public sealed class GranitAuthenticationJwtBearerCognitoModuleTests
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
        });

        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddGranitJwtBearer();

        GranitAuthenticationJwtBearerCognitoModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitAuthenticationJwtBearerCognitoModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void ConfigureServices_RegistersClaimsTransformation()
    {
        ServiceProvider provider = BuildProvider();

        IClaimsTransformation transformation = provider.GetRequiredService<IClaimsTransformation>();

        transformation.ShouldBeOfType<CognitoClaimsTransformation>();
    }

}
