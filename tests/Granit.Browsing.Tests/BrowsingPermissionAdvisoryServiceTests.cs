using Granit.Authorization;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests;

public sealed class BrowsingPermissionAdvisoryServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task GranitBrowsingModule_hosted_services_should_start_under_scope_validation()
    {
        // Regression: BrowsingPermissionAdvisoryService is a singleton hosted service
        // that resolves IPermissionChecker (scoped). Pulling it from the root provider
        // throws under ValidateScopes — it must create a transient scope per call.
        ServiceCollection services = new();
        ConfigurationBuilder cfgBuilder = new();
        IConfiguration configuration = cfgBuilder.Build();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddMetrics();
        services.AddScoped(_ => Substitute.For<IPermissionChecker>());

        HostApplicationBuilder hostBuilder = Host.CreateEmptyApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        ServiceConfigurationContext context = new(services, configuration, hostBuilder);
        new GranitBrowsingModule().ConfigureServices(context);

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();
        hostedServices.ShouldNotBeEmpty();

        foreach (IHostedService hosted in hostedServices)
        {
            await Should.NotThrowAsync(() => hosted.StartAsync(CancellationToken.None));
            await hosted.StopAsync(CancellationToken.None);
        }
    }
}
