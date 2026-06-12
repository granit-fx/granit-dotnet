using Granit.IpGeolocation;
using Granit.UserSessions.Internal;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Abstractions.Tests;

public sealed class NullSessionAnomalyDetectorTests
{
    [Fact]
    public async Task AssessAsync_AlwaysReturnsNone()
    {
        NullSessionAnomalyDetector sut = new();
        SessionDescriptor candidate = new(
            "session-1",
            "user-1",
            IsCurrent: true,
            CreatedAt: DateTimeOffset.UnixEpoch,
            LastAccessedAt: null,
            UserAgent: null,
            IpAddress: "8.8.8.8",
            Location: new GeoLocation { CountryCode = "US" });

        SessionRiskAssessment result = await sut.AssessAsync(candidate, [], TestContext.Current.CancellationToken);

        result.ShouldBe(SessionRiskAssessment.None);
        result.Level.ShouldBe(SessionRiskLevel.None);
    }
}
