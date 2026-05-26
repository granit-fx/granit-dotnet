using System.Security.Claims;
using Granit.Privacy.Endpoints.Internal;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Internal;

public sealed class PrivacyStepUpChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    [Fact]
    public void IsAuthFresh_ReturnsTrue_WhenAuthTimeIsWithinWindow()
    {
        long authTime = Now.AddMinutes(-3).ToUnixTimeSeconds();
        DefaultHttpContext context = BuildContextWithAuthTimeClaim(authTime.ToString(System.Globalization.CultureInfo.InvariantCulture));

        PrivacyStepUpChallenge.IsAuthFresh(context, FiveMinutes, Now).ShouldBeTrue();
    }

    [Fact]
    public void IsAuthFresh_ReturnsFalse_WhenAuthTimeIsBeyondWindow()
    {
        long authTime = Now.AddMinutes(-10).ToUnixTimeSeconds();
        DefaultHttpContext context = BuildContextWithAuthTimeClaim(authTime.ToString(System.Globalization.CultureInfo.InvariantCulture));

        PrivacyStepUpChallenge.IsAuthFresh(context, FiveMinutes, Now).ShouldBeFalse();
    }

    [Fact]
    public void IsAuthFresh_ReturnsTrue_AtExactBoundary()
    {
        // The check is "now - authTime <= maxAge", i.e. 5 minutes ago is still fresh.
        long authTime = Now.AddMinutes(-5).ToUnixTimeSeconds();
        DefaultHttpContext context = BuildContextWithAuthTimeClaim(authTime.ToString(System.Globalization.CultureInfo.InvariantCulture));

        PrivacyStepUpChallenge.IsAuthFresh(context, FiveMinutes, Now).ShouldBeTrue();
    }

    [Fact]
    public void IsAuthFresh_ReturnsFalse_WhenAuthTimeClaimMissing()
    {
        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));

        PrivacyStepUpChallenge.IsAuthFresh(context, FiveMinutes, Now).ShouldBeFalse();
    }

    [Fact]
    public void IsAuthFresh_ReturnsFalse_WhenAuthTimeClaimIsNonNumeric()
    {
        DefaultHttpContext context = BuildContextWithAuthTimeClaim("not-a-number");

        PrivacyStepUpChallenge.IsAuthFresh(context, FiveMinutes, Now).ShouldBeFalse();
    }

    [Fact]
    public void SetStepUpChallengeHeader_SetsWwwAuthenticateBearerStepUp()
    {
        DefaultHttpContext context = new();

        string detail = PrivacyStepUpChallenge.SetStepUpChallengeHeader(context, FiveMinutes);

        detail.ShouldContain("Step-up authentication required");
        context.Response.Headers["WWW-Authenticate"].ToString()
            .ShouldBe("Bearer error=\"step_up\", acr_values=\"urn:granit:step-up\", max_age=\"300\"");
    }

    private static DefaultHttpContext BuildContextWithAuthTimeClaim(string claimValue)
    {
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new(authenticationType: "test");
        identity.AddClaim(new Claim("auth_time", claimValue));
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
