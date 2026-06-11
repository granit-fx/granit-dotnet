// =============================================================================
// Tests - GeoValidatorExtensions
// =============================================================================
// GeoLatitude:  -90 to +90
// GeoLongitude: -180 to +180
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class GeoValidatorExtensionsTests
{
    // =========================================================================
    // GeoLatitude (double)
    // =========================================================================

    [Theory]
    [InlineData(0.0)]
    [InlineData(90.0)]
    [InlineData(-90.0)]
    [InlineData(48.8566)]                       // Paris
    [InlineData(-33.8688)]                      // Sydney
    public void GeoLatitude_ValidValues_PassValidation(double lat)
    {
        InlineValidator<GeoModel> validator = [];
        validator.RuleFor(x => x.Latitude).GeoLatitude();

        ValidationResult result = validator.Validate(new GeoModel(lat, 0));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(90.1)]
    [InlineData(-90.1)]
    [InlineData(180.0)]
    [InlineData(-180.0)]
    [InlineData(double.MaxValue)]
    public void GeoLatitude_InvalidValues_FailValidation(double lat)
    {
        InlineValidator<GeoModel> validator = [];
        validator.RuleFor(x => x.Latitude).GeoLatitude();

        ValidationResult result = validator.Validate(new GeoModel(lat, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:GeoLatitude");
    }

    // =========================================================================
    // GeoLongitude (double)
    // =========================================================================

    [Theory]
    [InlineData(0.0)]
    [InlineData(180.0)]
    [InlineData(-180.0)]
    [InlineData(2.3522)]                        // Paris
    [InlineData(151.2093)]                      // Sydney
    public void GeoLongitude_ValidValues_PassValidation(double lng)
    {
        InlineValidator<GeoModel> validator = [];
        validator.RuleFor(x => x.Longitude).GeoLongitude();

        ValidationResult result = validator.Validate(new GeoModel(0, lng));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(180.1)]
    [InlineData(-180.1)]
    [InlineData(360.0)]
    [InlineData(double.MaxValue)]
    public void GeoLongitude_InvalidValues_FailValidation(double lng)
    {
        InlineValidator<GeoModel> validator = [];
        validator.RuleFor(x => x.Longitude).GeoLongitude();

        ValidationResult result = validator.Validate(new GeoModel(0, lng));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:GeoLongitude");
    }

    // =========================================================================
    // GeoLatitude (decimal)
    // =========================================================================

    [Theory]
    [InlineData("0")]
    [InlineData("90")]
    [InlineData("-90")]
    [InlineData("48.8566")]
    public void GeoLatitude_Decimal_ValidValues_PassValidation(string latStr)
    {
        decimal lat = decimal.Parse(latStr, System.Globalization.CultureInfo.InvariantCulture);
        InlineValidator<GeoDecimalModel> validator = [];
        validator.RuleFor(x => x.Latitude).GeoLatitude();

        ValidationResult result = validator.Validate(new GeoDecimalModel(lat, 0));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("90.1")]
    [InlineData("-90.1")]
    public void GeoLatitude_Decimal_InvalidValues_FailValidation(string latStr)
    {
        decimal lat = decimal.Parse(latStr, System.Globalization.CultureInfo.InvariantCulture);
        InlineValidator<GeoDecimalModel> validator = [];
        validator.RuleFor(x => x.Latitude).GeoLatitude();

        ValidationResult result = validator.Validate(new GeoDecimalModel(lat, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:GeoLatitude");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record GeoModel(double Latitude, double Longitude);
    private sealed record GeoDecimalModel(decimal Latitude, decimal Longitude);
}
