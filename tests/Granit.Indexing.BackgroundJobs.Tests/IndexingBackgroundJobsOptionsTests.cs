using Granit.Indexing.BackgroundJobs.Options;
using Shouldly;
using Xunit;

namespace Granit.Indexing.BackgroundJobs.Tests;

public sealed class IndexingBackgroundJobsOptionsTests
{
    [Fact]
    public void SectionName_matches_namespace_aligned_canonical_path() =>
        // Locked here so a rename of the project / namespace surfaces in CI before
        // it silently breaks host appsettings.json bindings.
        IndexingBackgroundJobsOptions.SectionName.ShouldBe("Indexing:BackgroundJobs");

    [Fact]
    public void Defaults_are_safe_for_production()
    {
        var options = new IndexingBackgroundJobsOptions();

        options.CheckpointBatchSize.ShouldBe(100);
        options.MaxConsecutiveFailures.ShouldBe(50);
        // Budgets default to null (unbounded) — the framework cannot pick a sensible
        // numeric default that fits every host. Hosts opt in via configuration.
        options.MaxEntriesPerRun.ShouldBeNull();
        options.MaxRunDurationSeconds.ShouldBeNull();
    }
}
