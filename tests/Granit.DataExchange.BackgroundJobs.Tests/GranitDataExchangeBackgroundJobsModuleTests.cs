using Granit.BackgroundJobs;
using Granit.DataExchange.BackgroundJobs.Options;
using Granit.DataExchange.BackgroundJobs.Services;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Retention;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.BackgroundJobs.Tests;

public sealed class GranitDataExchangeBackgroundJobsModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule() =>
        new GranitDataExchangeBackgroundJobsModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_DependsOnBackgroundJobsAndDataExchangeModules()
    {
        DependsOnAttribute[] attributes = typeof(GranitDataExchangeBackgroundJobsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitBackgroundJobsModule)));
        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitDataExchangeModule)));
    }

    [Fact]
    public void ConfigureServices_RegistersRetentionSweepService()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);

        new GranitDataExchangeBackgroundJobsModule().ConfigureServices(context);

        builder.Services.ShouldContain(d => d.ServiceType == typeof(RetentionSweepService));
    }

    [Fact]
    public void ConfigureServices_RegistersOptionsValidator()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);

        new GranitDataExchangeBackgroundJobsModule().ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<DataExchangeRetentionOptions>) &&
            d.ImplementationType == typeof(DataExchangeRetentionOptionsValidator));
    }

    [Fact]
    public void ConfigureServices_ResolvesRetentionSweepServiceAndValidator()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["DataExchange:Retention:SweepBatchSize"] = "250";
        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);

        new GranitDataExchangeBackgroundJobsModule().ConfigureServices(context);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddMetrics();
        builder.Services.AddSingleton<DataExchangeMetrics>();
        builder.Services.AddSingleton(Substitute.For<IDataExchangeFileProvider>());
        builder.Services.AddSingleton(Substitute.For<IDataExchangeRetentionStore>());

        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<RetentionSweepService>().ShouldNotBeNull();
        provider.GetRequiredService<IValidateOptions<DataExchangeRetentionOptions>>().ShouldNotBeNull();
        provider.GetRequiredService<IOptions<DataExchangeRetentionOptions>>().Value.SweepBatchSize.ShouldBe(250);
    }
}
