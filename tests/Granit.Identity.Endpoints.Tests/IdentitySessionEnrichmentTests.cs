using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Models;
using Granit.IpGeolocation;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

public sealed class IdentitySessionEnrichmentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IdentitySession Session(string id, string? ip) =>
        new(id, ip, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, RememberMe: false, Clients: ["app"]);

    [Fact]
    public async Task EnrichSessionsAsync_MasksIpAndAddsLocationAndRisk_ByDefault()
    {
        IIpGeolocationResolver geo = Substitute.For<IIpGeolocationResolver>();
        geo.ResolveAsync("203.0.113.7", Arg.Any<CancellationToken>())
            .Returns(new GeoLocation { City = "Brussels", CountryCode = "BE" });

        IUserSessionRiskStore risk = Substitute.For<IUserSessionRiskStore>();
        risk.GetManyAsync("user-1", Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, UserSessionRiskVerdict>
            {
                ["s1"] = new(UserSessionRiskLevel.High, ["impossible_travel"], DateTimeOffset.UnixEpoch),
            });

        List<IdentitySessionResponse> result = await IdentitySessionEnrichment.EnrichSessionsAsync(
            [Session("s1", "203.0.113.7")], "user-1", geo, risk, exposeRawIp: false, Ct);

        result.Count.ShouldBe(1);
        result[0].IpAddress.ShouldBe("203.0.113.0"); // masked
        result[0].Location!.City.ShouldBe("Brussels");
        result[0].RiskLevel.ShouldBe(UserSessionRiskLevel.High);
    }

    [Fact]
    public async Task EnrichSessionsAsync_ExposeRawIp_KeepsFullIp()
    {
        IIpGeolocationResolver geo = Substitute.For<IIpGeolocationResolver>();
        IUserSessionRiskStore risk = Substitute.For<IUserSessionRiskStore>();
        risk.GetManyAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, UserSessionRiskVerdict>());

        List<IdentitySessionResponse> result = await IdentitySessionEnrichment.EnrichSessionsAsync(
            [Session("s1", "203.0.113.7")], "user-1", geo, risk, exposeRawIp: true, Ct);

        result[0].IpAddress.ShouldBe("203.0.113.7");
        result[0].RiskLevel.ShouldBeNull();
    }
}
