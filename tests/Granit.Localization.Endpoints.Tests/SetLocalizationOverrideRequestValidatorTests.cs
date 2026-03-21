using FluentValidation.Results;
using Granit.Localization.Endpoints.Dtos;
using Granit.Localization.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class SetLocalizationOverrideRequestValidatorTests
{
    private readonly SetLocalizationOverrideRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidValue_ReturnsValid()
    {
        SetLocalizationOverrideRequest request = new("Bonjour");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyValue_ReturnsInvalid()
    {
        SetLocalizationOverrideRequest request = new("");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value");
    }

    [Fact]
    public void Validate_WithWhitespaceValue_ReturnsInvalid()
    {
        SetLocalizationOverrideRequest request = new("   ");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithValueExceedingMaxLength_ReturnsInvalid()
    {
        string longValue = new('x', SetLocalizationOverrideRequestValidator.MaxValueLength + 1);
        SetLocalizationOverrideRequest request = new(longValue);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value");
    }

    [Fact]
    public void Validate_WithValueAtMaxLength_ReturnsValid()
    {
        string maxValue = new('x', SetLocalizationOverrideRequestValidator.MaxValueLength);
        SetLocalizationOverrideRequest request = new(maxValue);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void MaxValueLength_Is4000() => SetLocalizationOverrideRequestValidator.MaxValueLength.ShouldBe(4000);
}
