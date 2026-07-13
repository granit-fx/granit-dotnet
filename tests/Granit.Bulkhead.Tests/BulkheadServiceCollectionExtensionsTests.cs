using Granit.Bulkhead.Diagnostics;
using Granit.Bulkhead.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Bulkhead.Tests;

public sealed class BulkheadServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateServices(Dictionary<string, string?>? config = null)
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? [])
            .Build();
        services.AddSingleton(configuration);
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }

    [Fact]
    public void AddGranitBulkhead_RegistersCoreServices()
    {
        ServiceCollection services = CreateServices(new Dictionary<string, string?>
        {
            ["Bulkhead:Policies:api:PermitLimit"] = "10",
        });

        services.AddGranitBulkhead();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<ConcurrencyLimiterRegistry>().ShouldNotBeNull();
        provider.GetService<BulkheadMetrics>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitBulkhead_WithConfigureAction_AppliesConfiguration()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitBulkhead(opts => opts.Enabled = false);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<ConcurrencyLimiterRegistry>().ShouldNotBeNull();
    }
}
