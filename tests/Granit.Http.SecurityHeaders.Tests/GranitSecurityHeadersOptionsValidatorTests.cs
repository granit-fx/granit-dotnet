using Granit.Http.SecurityHeaders.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

public sealed class GranitSecurityHeadersOptionsValidatorTests
{
    private readonly GranitSecurityHeadersOptionsValidator _validator = new();

    [Fact]
    public void DefaultOptions_AreValid()
    {
        ValidateOptionsResult result = _validator.Validate(null, new GranitSecurityHeadersOptions());

        result.Succeeded.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // X-Frame-Options
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("DENY")]
    [InlineData("SAMEORIGIN")]
    [InlineData("deny")]
    [InlineData("Sameorigin")]
    public void XFrameOptions_ValidValues_Pass(string value) =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { XFrameOptions = value })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void XFrameOptions_Null_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { XFrameOptions = null })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void XFrameOptions_Invalid_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { XFrameOptions = "INVALID" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("XFrameOptions");
    }

    // -------------------------------------------------------------------------
    // Referrer-Policy
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("no-referrer")]
    [InlineData("strict-origin-when-cross-origin")]
    [InlineData("unsafe-url")]
    [InlineData("origin-when-cross-origin")]
    public void ReferrerPolicy_ValidValues_Pass(string value) =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { ReferrerPolicy = value })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void ReferrerPolicy_Empty_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { ReferrerPolicy = "" })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void ReferrerPolicy_Invalid_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { ReferrerPolicy = "none" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ReferrerPolicy");
    }

    // -------------------------------------------------------------------------
    // Cross-Origin-Opener-Policy
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("same-origin")]
    [InlineData("same-origin-allow-popups")]
    [InlineData("unsafe-none")]
    public void CrossOriginOpenerPolicy_ValidValues_Pass(string value) =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { CrossOriginOpenerPolicy = value })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void CrossOriginOpenerPolicy_Empty_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { CrossOriginOpenerPolicy = "" })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void CrossOriginOpenerPolicy_Invalid_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { CrossOriginOpenerPolicy = "invalid" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CrossOriginOpenerPolicy");
    }

    // -------------------------------------------------------------------------
    // Cross-Origin-Embedder-Policy
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("require-corp")]
    [InlineData("credentialless")]
    [InlineData("unsafe-none")]
    public void CrossOriginEmbedderPolicy_ValidValues_Pass(string value) =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { CrossOriginEmbedderPolicy = value })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void CrossOriginEmbedderPolicy_Null_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { CrossOriginEmbedderPolicy = null })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void CrossOriginEmbedderPolicy_Invalid_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { CrossOriginEmbedderPolicy = "invalid" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CrossOriginEmbedderPolicy");
    }

    // -------------------------------------------------------------------------
    // Cross-Origin-Resource-Policy
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("same-origin")]
    [InlineData("same-site")]
    [InlineData("cross-origin")]
    public void CrossOriginResourcePolicy_ValidValues_Pass(string value) =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { CrossOriginResourcePolicy = value })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void CrossOriginResourcePolicy_Empty_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { CrossOriginResourcePolicy = "" })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void CrossOriginResourcePolicy_Invalid_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { CrossOriginResourcePolicy = "invalid" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CrossOriginResourcePolicy");
    }

    // -------------------------------------------------------------------------
    // HSTS max-age
    // -------------------------------------------------------------------------

    // When HSTS is enabled, reject values below the OWASP
    // 6-month minimum. A `max-age=0` with EnableHsts=true silently
    // instructs browsers to forget HSTS, which is a downgrade a misconfig
    // must not be able to cause.

    [Fact]
    public void HstsMaxAgeSeconds_NegativeWithHstsDisabled_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { EnableHsts = false, HstsMaxAgeSeconds = -1 });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HstsMaxAgeSeconds");
    }

    [Fact]
    public void HstsMaxAgeSeconds_ZeroWithHstsEnabled_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { EnableHsts = true, HstsMaxAgeSeconds = 0 });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HstsMaxAgeSeconds");
    }

    [Fact]
    public void HstsMaxAgeSeconds_BelowSixMonthsWithHstsEnabled_Fails()
    {
        ValidateOptionsResult result = _validator.Validate(
            null, new GranitSecurityHeadersOptions { EnableHsts = true, HstsMaxAgeSeconds = 3600 });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HstsMaxAgeSeconds");
    }

    [Fact]
    public void HstsMaxAgeSeconds_ZeroWithHstsDisabled_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { EnableHsts = false, HstsMaxAgeSeconds = 0 })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void HstsMaxAgeSeconds_DefaultOneYear_Passes() =>
        _validator.Validate(null, new GranitSecurityHeadersOptions { EnableHsts = true, HstsMaxAgeSeconds = 31_536_000 })
            .Succeeded.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Multiple failures
    // -------------------------------------------------------------------------

    [Fact]
    public void MultipleInvalidValues_ReportsAllFailures()
    {
        // EnableHsts=false + negative HstsMaxAge still fails the "must be >= 0" check.
        ValidateOptionsResult result = _validator.Validate(null, new GranitSecurityHeadersOptions
        {
            XFrameOptions = "INVALID",
            ReferrerPolicy = "none",
            EnableHsts = false,
            HstsMaxAgeSeconds = -1,
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("XFrameOptions");
        result.FailureMessage.ShouldContain("ReferrerPolicy");
        result.FailureMessage.ShouldContain("HstsMaxAgeSeconds");
    }
}
