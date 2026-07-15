using Granit.Imaging.MagickNet.Extensions;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests;

public sealed class ImagingMagickNetServiceRegistrationTests
{
    private static HostApplicationBuilder CreateBuilder(params KeyValuePair<string, string?>[] config)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection(config);
        builder.Services.AddLogging();
        builder.Services.AddMetrics();
        return builder;
    }

    [Fact]
    public void AddGranitImagingMagickNet_registers_IImageProcessor_as_singleton()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGranitImagingMagickNet();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IImageProcessor) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitImagingMagickNet_second_call_does_not_duplicate_registrations()
    {
        // The module's ConfigureServices and an explicit app call may both run — the
        // registration must be idempotent (this was the source of the silent options
        // loss the config-bound rewrite eliminates).
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGranitImagingMagickNet();
        builder.AddGranitImagingMagickNet();

        builder.Services.Count(d => d.ServiceType == typeof(IImageProcessor)).ShouldBe(1);
        builder.Services
            .Count(d => d.ServiceType == typeof(IHostedService) &&
                        d.ImplementationType == typeof(MagickNetResourceLimitsInitializer))
            .ShouldBe(1);
        builder.Services
            .Count(d => d.ServiceType == typeof(IValidateOptions<ImagingMagickNetOptions>))
            .ShouldBe(1);
    }

    [Fact]
    public void Options_bind_from_the_ImagingMagickNet_configuration_section()
    {
        HostApplicationBuilder builder = CreateBuilder(
            new KeyValuePair<string, string?>("Imaging:MagickNet:MaxInputBytes", "1024"));
        builder.AddGranitImagingMagickNet();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ImagingMagickNetOptions>>()
            .Value.MaxInputBytes.ShouldBe(1024);
    }

    [Fact]
    public void Configure_callback_wins_over_bound_configuration()
    {
        HostApplicationBuilder builder = CreateBuilder(
            new KeyValuePair<string, string?>("Imaging:MagickNet:MaxInputBytes", "1024"));
        builder.AddGranitImagingMagickNet(options => options.MaxInputBytes = 2048);

        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ImagingMagickNetOptions>>()
            .Value.MaxInputBytes.ShouldBe(2048);
    }

    [Fact]
    public void Invalid_configuration_fails_options_validation()
    {
        HostApplicationBuilder builder = CreateBuilder(
            new KeyValuePair<string, string?>("Imaging:MagickNet:MaxInputBytes", "-1"));
        builder.AddGranitImagingMagickNet();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<ImagingMagickNetOptions>>().Value);
    }

    [Fact]
    public void Registration_passes_scope_validation()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGranitImagingMagickNet();

        Should.NotThrow(() =>
        {
            using ServiceProvider provider = builder.Services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            provider.GetRequiredService<IImageProcessor>().ShouldNotBeNull();
        });
    }
}
