using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Options;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Options;

public sealed class BackgroundJobsOptionsTests
{
    [Fact]
    public void SectionName_IsBackgroundJobs() => BackgroundJobsOptions.SectionName.ShouldBe("BackgroundJobs");

    [Fact]
    public void Mode_Default_IsInMemory()
    {
        BackgroundJobsOptions options = new();

        options.Mode.ShouldBe(JobStoreMode.InMemory);
    }

    [Fact]
    public void ConnectionString_Default_IsEmpty()
    {
        BackgroundJobsOptions options = new();

        options.ConnectionString.ShouldBe(string.Empty);
    }

    [Fact]
    public void Mode_CanBeSetToDurable()
    {
        BackgroundJobsOptions options = new() { Mode = JobStoreMode.Durable };

        options.Mode.ShouldBe(JobStoreMode.Durable);
    }

    [Fact]
    public void ConnectionString_CanBeSet()
    {
        BackgroundJobsOptions options = new() { ConnectionString = "Host=localhost;Database=test;" };

        options.ConnectionString.ShouldBe("Host=localhost;Database=test;");
    }
}
