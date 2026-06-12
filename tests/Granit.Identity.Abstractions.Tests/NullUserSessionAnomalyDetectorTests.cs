using Granit.Identity.Internal;
using Granit.IpGeolocation;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class NullUserSessionAnomalyDetectorTests
{
    [Fact]
    public async Task AssessAsync_AlwaysReturnsNone()
    {
        NullUserSessionAnomalyDetector sut = new();
        UserSessionDescriptor candidate = new(
            "session-1",
            "user-1",
            IsCurrent: true,
            CreatedAt: DateTimeOffset.UnixEpoch,
            LastAccessedAt: null,
            UserAgent: null,
            IpAddress: "8.8.8.8",
            Location: new GeoLocation { CountryCode = "US" });

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [], TestContext.Current.CancellationToken);

        result.ShouldBe(UserSessionRiskAssessment.None);
        result.Level.ShouldBe(UserSessionRiskLevel.None);
    }
}
