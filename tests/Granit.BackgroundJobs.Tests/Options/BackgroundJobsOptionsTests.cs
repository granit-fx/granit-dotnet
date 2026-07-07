using Granit.BackgroundJobs.Options;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Options;

public sealed class BackgroundJobsOptionsTests
{
    [Fact]
    public void SectionName_IsBackgroundJobs() => BackgroundJobsOptions.SectionName.ShouldBe("BackgroundJobs");

    [Fact]
    public void FailureAlertThreshold_Default_IsThree()
    {
        BackgroundJobsOptions options = new();

        options.FailureAlertThreshold.ShouldBe(3);
    }

    [Fact]
    public void FailureAlertThreshold_CanBeSet()
    {
        BackgroundJobsOptions options = new() { FailureAlertThreshold = 5 };

        options.FailureAlertThreshold.ShouldBe(5);
    }
}
