// =============================================================================
// Tests - ContactValidatorExtensions
// =============================================================================
// Email: RFC-compliant via FluentValidation .EmailAddress()
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class ContactValidatorExtensionsTests
{
    // =========================================================================
    // Email
    // =========================================================================

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name@domain.org")]
    [InlineData("user+label@sub.domain.be")]
    [InlineData("admin@granit.io")]
    public void Email_ValidValues_PassValidation(string email)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Email();

        ValidationResult result = validator.Validate(new TestModel(email));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notanemail")]              // no @ sign
    [InlineData("missing@")]               // no domain
    [InlineData("@nodomain.com")]          // no local part
    [InlineData("spaces in@email.com")]    // space in local part
    public void Email_InvalidValues_FailValidation(string? email)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Email();

        ValidationResult result = validator.Validate(new TestModel(email));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Granit:Validation:InvalidEmail");
        result.Errors[0].ErrorCode.ShouldBe("Granit:Validation:InvalidEmail");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
