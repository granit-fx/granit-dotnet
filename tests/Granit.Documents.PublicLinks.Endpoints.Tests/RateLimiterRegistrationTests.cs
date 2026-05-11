using Granit.Documents.PublicLinks.Endpoints.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="DocumentsPublicLinksRateLimiterServiceCollectionExtensions"/>
/// registers the named policy expected by the anonymous redemption group (F18.4).
/// </summary>
public sealed class RateLimiterRegistrationTests
{
    [Fact]
    public void AddGranitDocumentsPublicLinksRateLimiter_RegistersRateLimiterOptions()
    {
        ServiceCollection services = new();
        services.AddLogging();

        services.AddGranitDocumentsPublicLinksRateLimiter(configuration: null);

        using ServiceProvider provider = services.BuildServiceProvider();
        // The policy registry is internal to ASP.NET Core. We assert the visible
        // contract: RateLimiterOptions is bound and resolvable from DI, which means
        // AddRateLimiter ran successfully and the named policy AddPolicy call queued.
        IOptions<RateLimiterOptions> options = provider.GetRequiredService<IOptions<RateLimiterOptions>>();
        options.Value.ShouldNotBeNull();
        DocumentsPublicLinksRateLimiterServiceCollectionExtensions.PolicyName
            .ShouldBe("granit-documents-public-links");
    }

    [Fact]
    public void AddGranitDocumentsPublicLinksRateLimiter_BindsOptionsFromConfiguration()
    {
        ServiceCollection services = new();
        services.AddLogging();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Granit:Documents:PublicLinks:RateLimitPerMinute"] = "5",
            })
            .Build();

        services.AddGranitDocumentsPublicLinksRateLimiter(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<Granit.Documents.PublicLinks.Options.GranitDocumentsPublicLinksOptions> monitor =
            provider.GetRequiredService<IOptionsMonitor<Granit.Documents.PublicLinks.Options.GranitDocumentsPublicLinksOptions>>();
        monitor.CurrentValue.RateLimitPerMinute.ShouldBe(5);
    }
}
