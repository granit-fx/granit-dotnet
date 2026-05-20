using Granit.Bff.Options;
using Granit.Bff.Yarp.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Bff.Yarp.Tests.Extensions;

public sealed class BffYarpHostApplicationBuilderExtensionsTests
{
    /// <summary>
    /// Regression: when both <see cref="GranitBffModule.ConfigureServices"/> (which now owns
    /// the canonical <c>Bff</c> section binding) and <see cref="BffYarpHostApplicationBuilderExtensions.AddGranitBffYarp"/>
    /// run against the same host, the <see cref="GranitBffOptions.Frontends"/> list must contain
    /// exactly one entry per configured frontend — not two.
    /// </summary>
    /// <remarks>
    /// Before the fix, <c>AddGranitBffYarp</c> also called <c>Configure&lt;GranitBffOptions&gt;</c>
    /// on the same section. <c>IConfiguration.Bind</c> appends to <c>List&lt;T&gt;</c> properties on
    /// every invocation, so a configured frontend would surface twice — duplicating the routes
    /// mapped by <c>MapGranitBff</c> and throwing
    /// <c>InvalidOperationException: Duplicate endpoint name 'BffLogin_&lt;name&gt;'</c> at startup.
    /// </remarks>
    [Fact]
    public void AddGranitBffYarp_CombinedWithBffModule_DoesNotDuplicateFrontends()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bff:Authority"] = "https://auth.example.com",
            ["Bff:Frontends:0:Name"] = "admin",
            ["Bff:Frontends:0:ClientId"] = "admin-client",
            ["Bff:Frontends:0:ClientSecret"] = "secret",
            ["ReverseProxy:Routes:r1:ClusterId"] = "c1",
            ["ReverseProxy:Routes:r1:Match:Path"] = "/{**catch-all}",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://backend",
        });

        // Stand in for the modularity pipeline: GranitBffModule owns the canonical binding.
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitBffModule().ConfigureServices(context);

        builder.AddGranitBffYarp();

        ServiceProvider provider = builder.Services.BuildServiceProvider();
        GranitBffOptions options = provider.GetRequiredService<IOptions<GranitBffOptions>>().Value;

        options.Frontends.Count.ShouldBe(1);
        options.Frontends[0].Name.ShouldBe("admin");
    }
}
