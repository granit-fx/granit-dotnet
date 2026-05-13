// =============================================================================
// Tests - GranitCookiesOptionsValidator
// =============================================================================

using Granit.Http.Cookies.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class GranitCookiesOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultConfig_Succeeds()
    {
        GranitCookiesOptionsValidator sut = new();

        ValidateOptionsResult result = sut.Validate(null, new GranitCookiesOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveRetention_Fails(int days)
    {
        GranitCookiesOptionsValidator sut = new();

        ValidateOptionsResult result = sut.Validate(null,
            new GranitCookiesOptions { DefaultRetentionDays = days });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GranitCookiesOptions.DefaultRetentionDays));
    }

    [Fact]
    public void Validate_ExceedsCnilCap_Fails()
    {
        GranitCookiesOptionsValidator sut = new();

        ValidateOptionsResult result = sut.Validate(null,
            new GranitCookiesOptions { DefaultRetentionDays = 730 }); // 2 years

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CNIL");
    }

    [Fact]
    public void Validate_EqualToMax_Succeeds()
    {
        GranitCookiesOptionsValidator sut = new();

        ValidateOptionsResult result = sut.Validate(null,
            new GranitCookiesOptions { DefaultRetentionDays = GranitCookiesOptions.MaxRetentionDays });

        result.Succeeded.ShouldBeTrue();
    }
}
