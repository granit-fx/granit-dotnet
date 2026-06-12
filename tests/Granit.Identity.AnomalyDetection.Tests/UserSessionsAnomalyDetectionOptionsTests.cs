using Granit.Identity.AnomalyDetection.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.AnomalyDetection.Tests;

public sealed class UserSessionsAnomalyDetectionOptionsTests
{
    [Fact]
    public void SectionName_IsNamespaceAlignedHierarchicalPath() =>
        UserSessionsAnomalyDetectionOptions.SectionName.ShouldBe("UserSessions:AnomalyDetection");

    [Fact]
    public void Defaults_AreWithinValidatedRanges()
    {
        UserSessionsAnomalyDetectionOptions options = new();

        options.UseAi.ShouldBeFalse();
        options.MaxAiCallsPerHourPerTenant.ShouldBe(500);
        options.MaxAiCallsPerHourPerUser.ShouldBe(50);
        options.AiTimeoutSeconds.ShouldBe(15);
        options.MaxTravelKilometersPerHour.ShouldBe(1000d);
    }
}
