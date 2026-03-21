using FluentValidation.Results;
using Granit.Features.Endpoints.Dtos;
using Granit.Features.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests.Validators;

public sealed class SetFeatureOverrideRequestValidatorTests
{
    private readonly SetFeatureOverrideRequestValidator _validator = new();

    [Fact]
    public void Valid_Value_Passes()
    {
        SetFeatureOverrideRequest request = new("true");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Empty_Value_Fails()
    {
        SetFeatureOverrideRequest request = new("");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value");
    }

    [Fact]
    public void Null_Value_Fails()
    {
        SetFeatureOverrideRequest request = new(null!);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Value_ExceedsMaxLength_Fails()
    {
        string longValue = new('x', SetFeatureOverrideRequestValidator.MaxValueLength + 1);
        SetFeatureOverrideRequest request = new(longValue);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value");
    }

    [Fact]
    public void Value_AtMaxLength_Passes()
    {
        string maxValue = new('x', SetFeatureOverrideRequestValidator.MaxValueLength);
        SetFeatureOverrideRequest request = new(maxValue);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void MaxValueLength_Is2000() => SetFeatureOverrideRequestValidator.MaxValueLength.ShouldBe(2000);
}
