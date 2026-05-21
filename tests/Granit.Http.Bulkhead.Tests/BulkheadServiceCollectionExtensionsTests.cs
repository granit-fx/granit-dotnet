using Granit.Http.Bulkhead.Diagnostics;
using Granit.Http.Bulkhead.Exceptions;
using Granit.Http.Bulkhead.Extensions;
using Granit.Http.ExceptionHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitBulkhead_RegistersCoreServices()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:Bulkhead:Policies:api:PermitLimit"] = "10",
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddSingleton<IConfiguration>(configuration);

        services.AddGranitBulkhead();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<ConcurrencyLimiterRegistry>().ShouldNotBeNull();
        provider.GetService<BulkheadMetrics>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitBulkhead_RegistersExceptionStatusCodeMapper()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:Bulkhead:Policies:api:PermitLimit"] = "10",
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddSingleton<IConfiguration>(configuration);

        services.AddGranitBulkhead();

        ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IExceptionStatusCodeMapper> mappers = provider.GetServices<IExceptionStatusCodeMapper>();
        mappers.ShouldContain(m => m is BulkheadExceptionStatusCodeMapper);
    }

    [Fact]
    public void AddGranitBulkhead_WithConfigureAction_AppliesConfiguration()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(configuration);
        services.AddSingleton<IConfiguration>(configuration);

        services.AddGranitBulkhead(opts =>
        {
            opts.Enabled = false;
        });

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<ConcurrencyLimiterRegistry>().ShouldNotBeNull();
    }
}
