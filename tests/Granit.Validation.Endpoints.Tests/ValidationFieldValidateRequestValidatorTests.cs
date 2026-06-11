using FluentValidation.Results;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationFieldValidateRequestValidatorTests
{
    private readonly ValidationFieldValidateRequestValidator _sut = new();

    [Theory]
    [InlineData("Validation:Format:Iban", "BE68539007547034")]
    [InlineData("Validation:Format:Email", null)]
    [InlineData("Guava:Validation:Custom", "test")]
    public void ValidRequest_PassesValidation(string errorCode, string? value)
    {
        ValidationResult result = _sut.Validate(new ValidationFieldValidateRequest(errorCode, value));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyErrorCode_FailsValidation()
    {
        ValidationResult result = _sut.Validate(new ValidationFieldValidateRequest("", "value"));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ErrorCodeTooLong_FailsValidation()
    {
        string longCode = new('A', 129);
        ValidationResult result = _sut.Validate(new ValidationFieldValidateRequest(longCode, "value"));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ErrorCodeWithInvalidChars_FailsValidation()
    {
        ValidationResult result = _sut.Validate(
            new ValidationFieldValidateRequest("Validation:Invalid Iban", "value"));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ValueTooLong_FailsValidation()
    {
        string longValue = new('x', 501);
        ValidationResult result = _sut.Validate(
            new ValidationFieldValidateRequest("Validation:Format:Iban", longValue));
        result.IsValid.ShouldBeFalse();
    }
}
