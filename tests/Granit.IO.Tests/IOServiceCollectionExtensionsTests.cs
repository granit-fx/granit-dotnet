using Granit.Diagnostics;
using Granit.IO.Diagnostics;
using Granit.IO.Extensions;
using Granit.IO.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class IOServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTempFiles_RegistersFactory()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();

        services.AddGranitTempFiles();

        using ServiceProvider sp = services.BuildServiceProvider();

        sp.GetRequiredService<ITempFileFactory>().ShouldNotBeNull();
        sp.GetRequiredService<IOMetrics>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTempFiles_RegistersJanitor_AsHostedService()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();

        services.AddGranitTempFiles();

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddGranitTempFiles_ConfigureDelegate_Applied()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();

        services.AddGranitTempFiles(o =>
        {
            o.MaxSizeBytes = 1024;
            o.RunJanitor = false;
        });

        using ServiceProvider sp = services.BuildServiceProvider();
        TempFileOptions opts = sp.GetRequiredService<IOptions<TempFileOptions>>().Value;

        opts.MaxSizeBytes.ShouldBe(1024);
        opts.RunJanitor.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitTempFiles_NullServices_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            IOServiceCollectionExtensions.AddGranitTempFiles(null!));

    [Fact]
    public void AddGranitTempFiles_RegistersActivitySource()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();

        services.AddGranitTempFiles();

        GranitActivitySourceRegistry.GetRegisteredSources().ShouldContain("Granit.IO");
    }
}
