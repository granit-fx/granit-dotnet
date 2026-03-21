using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class GeoValidatorExtensionsAdditionalTests
{
    // =========================================================================
    // GeoLatitude (nullable double)
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(90.0)]
    [InlineData(-90.0)]
    [InlineData(48.8566)]
    public void GeoLatitude_NullableDouble_ValidValues_PassValidation(double? lat)
    {
        InlineValidator<NullableGeoModel> validator = [];
        validator.RuleFor(x => x.Latitude).GeoLatitude();

        ValidationResult result = validator.Validate(new NullableGeoModel(lat, null));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(90.1)]
    [InlineData(-90.1)]
    [InlineData(180.0)]
    public void GeoLatitude_NullableDouble_InvalidValues_FailValidation(double? lat)
    {
        InlineValidator<NullableGeoModel> validator = [];
        validator.RuleFor(x => x.Latitude).GeoLatitude();

        ValidationResult result = validator.Validate(new NullableGeoModel(lat, null));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Granit:Validation:InvalidGeoLatitude");
    }

    // =========================================================================
    // GeoLongitude (nullable double)
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(180.0)]
    [InlineData(-180.0)]
    [InlineData(2.3522)]
    public void GeoLongitude_NullableDouble_ValidValues_PassValidation(double? lng)
    {
        InlineValidator<NullableGeoModel> validator = [];
        validator.RuleFor(x => x.Longitude).GeoLongitude();

        ValidationResult result = validator.Validate(new NullableGeoModel(null, lng));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(180.1)]
    [InlineData(-180.1)]
    [InlineData(360.0)]
    public void GeoLongitude_NullableDouble_InvalidValues_FailValidation(double? lng)
    {
        InlineValidator<NullableGeoModel> validator = [];
        validator.RuleFor(x => x.Longitude).GeoLongitude();

        ValidationResult result = validator.Validate(new NullableGeoModel(null, lng));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Granit:Validation:InvalidGeoLongitude");
    }

    // =========================================================================
    // GeoLongitude (decimal)
    // =========================================================================

    [Theory]
    [InlineData("0")]
    [InlineData("180")]
    [InlineData("-180")]
    [InlineData("2.3522")]
    public void GeoLongitude_Decimal_ValidValues_PassValidation(string lngStr)
    {
        decimal lng = decimal.Parse(lngStr, System.Globalization.CultureInfo.InvariantCulture);
        InlineValidator<GeoDecimalModel> validator = [];
        validator.RuleFor(x => x.Longitude).GeoLongitude();

        ValidationResult result = validator.Validate(new GeoDecimalModel(0, lng));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("180.1")]
    [InlineData("-180.1")]
    public void GeoLongitude_Decimal_InvalidValues_FailValidation(string lngStr)
    {
        decimal lng = decimal.Parse(lngStr, System.Globalization.CultureInfo.InvariantCulture);
        InlineValidator<GeoDecimalModel> validator = [];
        validator.RuleFor(x => x.Longitude).GeoLongitude();

        ValidationResult result = validator.Validate(new GeoDecimalModel(0, lng));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Granit:Validation:InvalidGeoLongitude");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record NullableGeoModel(double? Latitude, double? Longitude);
    private sealed record GeoDecimalModel(decimal Latitude, decimal Longitude);
}
