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
    private static IOptions<TikaSidecarOptions> BuildOptions(
        TikaSidecarOptions setup,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        IHttpClientBuilder builder = services.AddTikaSidecarExtractor(opts =>
        {
            opts.Uri = setup.Uri;
            opts.AllowedHosts = setup.AllowedHosts;
            opts.RequireMutualTls = setup.RequireMutualTls;
            opts.RequireHttps = setup.RequireHttps;
            opts.SkipEmbeddedResources = setup.SkipEmbeddedResources;
            opts.AllowedContentTypes = setup.AllowedContentTypes;
        });
        configureClient?.Invoke(builder);
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
            RequireMutualTls = false,
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
            RequireMutualTls = false,
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
            RequireMutualTls = false,
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
            RequireMutualTls = false,
        });

        TikaSidecarOptions resolved = opts.Value;
        // URI host normalises to lowercase already; the validator still passes regardless.
        resolved.Uri.Host.ShouldBe("tika.internal");
    }

    // ──── VULN-104 — HTTPS scheme enforcement ─────────────────────────────────────

    [Fact]
    public void Http_scheme_on_non_localhost_uri_fails_validation_when_RequireHttps_true()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("http://tika.internal/"),
            AllowedHosts = ["tika.internal"],
            RequireHttps = true,
            RequireMutualTls = false,
        });

        OptionsValidationException ex = Should.Throw<OptionsValidationException>(() => opts.Value);
        ex.Message.ShouldContain("https://");
    }

    [Fact]
    public void Http_scheme_on_localhost_is_allowed_even_when_RequireHttps_true()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("http://localhost:9998/"),
            AllowedHosts = ["localhost"],
            RequireHttps = true,
            RequireMutualTls = false,
        });

        TikaSidecarOptions resolved = opts.Value;
        resolved.Uri.Scheme.ShouldBe("http");
    }

    [Fact]
    public void Http_scheme_is_allowed_when_RequireHttps_explicitly_false()
    {
        IOptions<TikaSidecarOptions> opts = BuildOptions(new TikaSidecarOptions
        {
            Uri = new Uri("http://tika.internal/"),
            AllowedHosts = ["tika.internal"],
            RequireHttps = false,
            RequireMutualTls = false,
        });

        TikaSidecarOptions resolved = opts.Value;
        resolved.Uri.Scheme.ShouldBe("http");
    }

    // ──── VULN-102 — RequireMutualTls enforcement ────────────────────────────────

    [Fact]
    public void RequireMutualTls_true_without_custom_handler_throws_when_client_is_created()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddTikaSidecarExtractor(opts =>
        {
            opts.Uri = new Uri("https://tika.internal/");
            opts.AllowedHosts = ["tika.internal"];
            opts.RequireMutualTls = true;
            // Crucially: no .ConfigurePrimaryHttpMessageHandler(...) call.
        });
        ServiceProvider sp = services.BuildServiceProvider();
        IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => factory.CreateClient(TikaSidecarTextExtractor.HttpClientName));
        ex.Message.ShouldContain("RequireMutualTls");
        ex.Message.ShouldContain("granit-tika");
    }

    [Fact]
    public void RequireMutualTls_true_with_custom_handler_allows_client_creation()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        IHttpClientBuilder builder = services.AddTikaSidecarExtractor(opts =>
        {
            opts.Uri = new Uri("https://tika.internal/");
            opts.AllowedHosts = ["tika.internal"];
            opts.RequireMutualTls = true;
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler());
        ServiceProvider sp = services.BuildServiceProvider();
        IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

        using System.Net.Http.HttpClient client = factory.CreateClient(TikaSidecarTextExtractor.HttpClientName);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void RequireMutualTls_false_allows_default_handler_chain()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddTikaSidecarExtractor(opts =>
        {
            opts.Uri = new Uri("https://tika.internal/");
            opts.AllowedHosts = ["tika.internal"];
            opts.RequireMutualTls = false;
        });
        ServiceProvider sp = services.BuildServiceProvider();
        IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

        using System.Net.Http.HttpClient client = factory.CreateClient(TikaSidecarTextExtractor.HttpClientName);

        client.ShouldNotBeNull();
    }
}
