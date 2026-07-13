using System.ComponentModel.DataAnnotations;
using Granit.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Tests.Extensions;

public sealed class GranitProviderOptionsServiceCollectionExtensionsTests
{
    private sealed class SampleProviderOptions
    {
        public const string SectionName = "Sample:Provider";

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;
    }

    private sealed class SampleCrossFieldValidator : IValidateOptions<SampleProviderOptions>
    {
        public ValidateOptionsResult Validate(string? name, SampleProviderOptions options) =>
            options.ApiKey.StartsWith("sk-", StringComparison.Ordinal)
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("ApiKey must start with 'sk-'.");
    }

    private static ServiceProvider Build(Dictionary<string, string?> config, bool withValidator = false)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());

        if (withValidator)
        {
            services.AddGranitProviderOptions<SampleProviderOptions, SampleCrossFieldValidator>(SampleProviderOptions.SectionName);
        }
        else
        {
            services.AddGranitProviderOptions<SampleProviderOptions>(SampleProviderOptions.SectionName);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void BindsFromTheGivenSection()
    {
        using ServiceProvider sp = Build(new()
        {
            ["Sample:Provider:ApiKey"] = "sk-abc",
            ["Sample:Provider:TimeoutSeconds"] = "60",
        });

        SampleProviderOptions opts = sp.GetRequiredService<IOptions<SampleProviderOptions>>().Value;

        opts.ApiKey.ShouldBe("sk-abc");
        opts.TimeoutSeconds.ShouldBe(60);
    }

    [Fact]
    public void DataAnnotations_AreEffective_MissingRequiredThrows()
    {
        using ServiceProvider sp = Build([]);

        Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<SampleProviderOptions>>().Value);
    }

    [Fact]
    public void DataAnnotations_AreEffective_OutOfRangeThrows()
    {
        using ServiceProvider sp = Build(new()
        {
            ["Sample:Provider:ApiKey"] = "sk-abc",
            ["Sample:Provider:TimeoutSeconds"] = "0",
        });

        Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<SampleProviderOptions>>().Value);
    }

    [Fact]
    public void CrossFieldValidator_IsRegisteredAndEnforced()
    {
        using ServiceProvider sp = Build(new()
        {
            ["Sample:Provider:ApiKey"] = "wrong-prefix",
        }, withValidator: true);

        OptionsValidationException ex = Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<SampleProviderOptions>>().Value);
        ex.Failures.ShouldContain(f => f.Contains("sk-"));
    }

    [Fact]
    public void RegistersValidateOnStart()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitProviderOptions<SampleProviderOptions>(SampleProviderOptions.SectionName);

        // ValidateOnStart materializes as an IStartupValidator registration.
        services.ShouldContain(d => d.ServiceType == typeof(IStartupValidator));
    }

    [Fact]
    public void ReturnsOptionsBuilderForChaining()
    {
        ServiceCollection services = new();

        OptionsBuilder<SampleProviderOptions> builder =
            services.AddGranitProviderOptions<SampleProviderOptions>(SampleProviderOptions.SectionName);

        builder.ShouldNotBeNull();
        Should.NotThrow(() => builder.Configure(o => o.TimeoutSeconds = 10));
    }

    [Fact]
    public void EmptySectionName_Throws()
    {
        ServiceCollection services = new();

        Should.Throw<ArgumentException>(
            () => services.AddGranitProviderOptions<SampleProviderOptions>(" "));
    }
}
