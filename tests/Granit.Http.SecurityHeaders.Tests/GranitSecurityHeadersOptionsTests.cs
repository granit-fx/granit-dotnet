using Granit.Http.SecurityHeaders.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

public sealed class GranitSecurityHeadersOptionsTests
{
    [Fact]
    public void SectionName_IsSecurityHeaders() =>
        GranitSecurityHeadersOptions.SectionName.ShouldBe("SecurityHeaders");

    [Fact]
    public void SuppressServerHeader_DefaultsToTrue() =>
        new GranitSecurityHeadersOptions().SuppressServerHeader.ShouldBeTrue();

    [Fact]
    public void EnableContentTypeOptions_DefaultsToTrue() =>
        new GranitSecurityHeadersOptions().EnableContentTypeOptions.ShouldBeTrue();

    [Fact]
    public void XFrameOptions_DefaultsToDeny() =>
        new GranitSecurityHeadersOptions().XFrameOptions.ShouldBe("DENY");

    [Fact]
    public void ReferrerPolicy_DefaultsToStrictOriginWhenCrossOrigin() =>
        new GranitSecurityHeadersOptions().ReferrerPolicy
            .ShouldBe("strict-origin-when-cross-origin");

    [Fact]
    public void DisableXssProtection_DefaultsToTrue() =>
        new GranitSecurityHeadersOptions().DisableXssProtection.ShouldBeTrue();

    [Fact]
    public void PermissionsPolicy_DefaultsToRestrictive() =>
        new GranitSecurityHeadersOptions().PermissionsPolicy
            .ShouldBe("camera=(), microphone=(), geolocation=(), payment=(), " +
                       "accelerometer=(), gyroscope=(), magnetometer=(), usb=()");

    [Fact]
    public void Csp_DefaultsToApiGradeStrictPolicy()
    {
        CspOptions csp = new GranitSecurityHeadersOptions().Csp;

        csp.DefaultSrc.ShouldBe(["'none'"]);
        csp.BaseUri.ShouldBe(["'none'"]);
        csp.FrameAncestors.ShouldBe(["'none'"]);
        csp.ScriptSrc.ShouldBeEmpty();
        csp.RawOverride.ShouldBeNull();
        csp.ReportOnly.ShouldBeFalse();
    }

    [Fact]
    public void DisabledContributors_DefaultsToEmpty() =>
        new GranitSecurityHeadersOptions().DisabledContributors.ShouldBeEmpty();

    [Fact]
    public void EnableHsts_DefaultsToTrue() =>
        new GranitSecurityHeadersOptions().EnableHsts.ShouldBeTrue();

    [Fact]
    public void HstsMaxAgeSeconds_DefaultsToOneYear() =>
        new GranitSecurityHeadersOptions().HstsMaxAgeSeconds.ShouldBe(31_536_000);

    [Fact]
    public void HstsIncludeSubDomains_DefaultsToTrue() =>
        new GranitSecurityHeadersOptions().HstsIncludeSubDomains.ShouldBeTrue();

    [Fact]
    public void HstsPreload_DefaultsToFalse() =>
        new GranitSecurityHeadersOptions().HstsPreload.ShouldBeFalse();

    [Fact]
    public void CrossOriginOpenerPolicy_DefaultsToSameOrigin() =>
        new GranitSecurityHeadersOptions().CrossOriginOpenerPolicy.ShouldBe("same-origin");

    [Fact]
    public void CrossOriginEmbedderPolicy_DefaultsToNull() =>
        new GranitSecurityHeadersOptions().CrossOriginEmbedderPolicy.ShouldBeNull();

    [Fact]
    public void CrossOriginResourcePolicy_DefaultsToSameOrigin() =>
        new GranitSecurityHeadersOptions().CrossOriginResourcePolicy.ShouldBe("same-origin");
}
