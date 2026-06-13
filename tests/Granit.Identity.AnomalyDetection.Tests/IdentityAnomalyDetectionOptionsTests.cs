using Granit.Identity.AnomalyDetection.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.AnomalyDetection.Tests;

public sealed class IdentityAnomalyDetectionOptionsTests
{
    [Fact]
    public void SectionName_IsNamespaceAlignedHierarchicalPath() =>
        IdentityAnomalyDetectionOptions.SectionName.ShouldBe("Identity:AnomalyDetection");

    [Fact]
    public void Defaults_AreWithinValidatedRanges()
    {
        IdentityAnomalyDetectionOptions options = new();

        options.UseAi.ShouldBeFalse();
        options.MaxAiCallsPerHourPerTenant.ShouldBe(500);
        options.MaxAiCallsPerHourPerUser.ShouldBe(50);
        options.AiTimeoutSeconds.ShouldBe(15);
        options.MaxTravelKilometersPerHour.ShouldBe(1000d);
    }
}
