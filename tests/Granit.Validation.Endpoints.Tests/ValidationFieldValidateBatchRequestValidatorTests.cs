using FluentValidation.Results;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationFieldValidateBatchRequestValidatorTests
{
    private readonly ValidationFieldValidateBatchRequestValidator _sut = new();

    [Fact]
    public void ValidBatch_PassesValidation()
    {
        var request = new ValidationFieldValidateBatchRequest(
        [
            new("Granit:Validation:InvalidIban", "BE68539007547034"),
            new("Granit:Validation:InvalidEmail", "test@example.com"),
        ]);

        ValidationResult result = _sut.Validate(request);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyFields_FailsValidation()
    {
        var request = new ValidationFieldValidateBatchRequest([]);
        ValidationResult result = _sut.Validate(request);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TooManyFields_FailsValidation()
    {
        var fields = Enumerable.Range(0, 21)
            .Select(i => new ValidationFieldValidateRequest($"Code{i}", "value"))
            .ToList();

        var request = new ValidationFieldValidateBatchRequest(fields);
        ValidationResult result = _sut.Validate(request);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ChildFieldWithInvalidErrorCode_FailsValidation()
    {
        var request = new ValidationFieldValidateBatchRequest(
        [
            new("", "value"),
        ]);

        ValidationResult result = _sut.Validate(request);
        result.IsValid.ShouldBeFalse();
    }
}
