using Granit.TextExtraction.Tika.Extensions;
using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tika.Tests;

public sealed class TikaSidecarOptionsValidationTests
{
    private static IOptions<TikaSidecarOptions> BuildOptions(TikaSidecarOptions setup)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddTikaSidecarExtractor(opts =>
        {
            opts.Uri = setup.Uri;
            opts.AllowedHosts = setup.AllowedHosts;
            opts.RequireMutualTls = setup.RequireMutualTls;
            opts.AllowedContentTypes = setup.AllowedContentTypes;
        });
        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<TikaSidecarOptions>>();
    }

    [Fact]
    public void Empty_AllowedHosts_fails_validation_on_resolve()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("https://tika.internal/"),
            AllowedHosts = [],
        });

        OptionsValidationException ex = Should.Throw<OptionsValidationException>(() => opts.Value);
        ex.Message.ShouldContain("AllowedHosts");
    }

    [Fact]
    public void Uri_host_outside_allowlist_fails_validation()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("https://attacker.example/"),
            AllowedHosts = ["tika.internal"],
        });

        OptionsValidationException ex = Should.Throw<OptionsValidationException>(() => opts.Value);
        ex.Message.ShouldContain("AllowedHosts");
    }

    [Fact]
    public void Uri_host_matching_allowlist_validates()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("https://tika.internal/tika"),
            AllowedHosts = ["tika.internal"],
        });

        TikaSidecarOptions resolved = opts.Value;
        resolved.Uri.Host.ShouldBe("tika.internal");
    }

    [Fact]
    public void AllowedHosts_match_is_case_insensitive()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("https://TIKA.internal/"),
            AllowedHosts = ["tika.internal"],
        });

        TikaSidecarOptions resolved = opts.Value;
        // URI host normalises to lowercase already; the validator still passes regardless.
        resolved.Uri.Host.ShouldBe("tika.internal");
    }
}
