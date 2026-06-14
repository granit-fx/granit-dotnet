using Granit.AI.Chat.BackgroundJobs.Options;
using Shouldly;
using Xunit;

namespace Granit.AI.Chat.BackgroundJobs.Tests;

public sealed class GranitAIChatRetentionOptionsTests
{
    [Fact]
    public void SectionName_matches_namespace_aligned_canonical_path()
    {
        // Locked here so a rename of the project / namespace surfaces in CI before
        // it silently breaks host appsettings.json bindings.
        GranitAIChatRetentionOptions.SectionName.ShouldBe("AI:Chat:Retention");
    }

    [Fact]
    public void Defaults_disable_retention_and_batch_in_bounded_chunks()
    {
        var options = new GranitAIChatRetentionOptions();

        // Retention is opt-in: 0 days means the cleanup job purges nothing.
        options.RetentionDays.ShouldBe(0);
        options.CleanupBatchSize.ShouldBe(500);
    }
}
