using Granit.Parties.EntityFrameworkCore.Deduplication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Deduplication;

public sealed class PartyDeduplicationOptionsTests
{
    [Fact]
    public void Defaults_match_the_research_baseline()
    {
        var opts = new PartyDeduplicationOptions();

        // Defaults documented in Epic #1278 prior-art research
        // (Salesforce / HubSpot / Dynamics 365 baselines).
        opts.NameSimilarityThreshold.ShouldBe(0.7);
        opts.CompanySimilarityThreshold.ShouldBe(0.6);
    }

    [Fact]
    public void Section_name_matches_the_documented_path()
    {
        // Drift guard: the upcoming Granit.Parties.Deduplication consumes IOptions<...>
        // and the appsettings docs reference this string. Renaming should be deliberate.
        PartyDeduplicationOptions.SectionName.ShouldBe("Granit:Parties:Deduplication");
    }

    [Fact]
    public void Binds_from_configuration_section()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Granit:Parties:Deduplication:NameSimilarityThreshold"] = "0.85",
                ["Granit:Parties:Deduplication:CompanySimilarityThreshold"] = "0.5",
            })
            .Build();

        ServiceCollection services = new();
        services.AddOptions<PartyDeduplicationOptions>()
            .Bind(config.GetSection(PartyDeduplicationOptions.SectionName));

        using ServiceProvider sp = services.BuildServiceProvider();
        PartyDeduplicationOptions resolved = sp.GetRequiredService<IOptions<PartyDeduplicationOptions>>().Value;

        resolved.NameSimilarityThreshold.ShouldBe(0.85);
        resolved.CompanySimilarityThreshold.ShouldBe(0.5);
    }
}
