using FluentValidation.Results;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Validators;

public sealed class PrivacyDeletionRequestValidatorTests
{
    private readonly PrivacyDeletionRequestValidator _validator = new();

    [Fact]
    public void Valid_Reason_Passes()
    {
        PrivacyDeletionRequest request = new("Account closure");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_Reason_Fails(string? reason)
    {
        PrivacyDeletionRequest request = new(reason!);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Reason");
    }

    [Fact]
    public void Reason_ExceedingMaxLength_Fails()
    {
        PrivacyDeletionRequest request = new(new string('x', 2001));

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Reason");
    }

    [Fact]
    public void Reason_AtMaxLength_Passes()
    {
        PrivacyDeletionRequest request = new(new string('x', 2000));

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
