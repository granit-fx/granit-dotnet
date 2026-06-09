using Granit.Bff.Options;
using Granit.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Bff.Endpoints.Tests;

public sealed class GranitBffEndpointsModuleTests
{
    [Fact]
    public void ConfigureServices_UsesDevelopmentCookiePrefix_ForNestedFrontends()
    {
        GranitBffOptions options = ConfigureAndResolveOptions(Environments.Development);

        options.Frontends.Single().SessionCookieName.ShouldBe(".bff-host");
    }

    [Fact]
    public void ConfigureServices_UsesHostCookiePrefix_ForNestedFrontends_OutsideDevelopment()
    {
        GranitBffOptions options = ConfigureAndResolveOptions(Environments.Production);

        options.Frontends.Single().SessionCookieName.ShouldBe("__Host-bff-host");
    }

    private static GranitBffOptions ConfigureAndResolveOptions(string environmentName)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environmentName });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bff:Authority"] = "https://auth.example.com",
            ["Bff:Frontends:0:Name"] = "host",
            ["Bff:Frontends:0:ClientId"] = "host-client",
            ["Bff:Frontends:0:ClientSecret"] = "host-secret",
            ["Bff:Frontends:0:PathPrefix"] = "/host",
        });

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitBffModule().ConfigureServices(context);
        new GranitBffEndpointsModule().ConfigureServices(context);

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<GranitBffOptions>>().Value;
    }
}
