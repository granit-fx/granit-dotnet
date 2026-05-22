using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class RuleBuilderExtensionsTests
{
    // =========================================================================
    // WithErrorCodeAndMessage
    // =========================================================================

    [Fact]
    public void WithErrorCodeAndMessage_SetsErrorCodeAndMessage()
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value)
            .Must(_ => false)
            .WithErrorCodeAndMessage("Validation:TestCode");

        ValidationResult result = validator.Validate(new TestModel("anything"));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:TestCode");
        result.Errors[0].ErrorMessage.ShouldBe("Validation:TestCode");
    }

    [Fact]
    public void WithErrorCodeAndMessage_ValidValue_NoErrors()
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value)
            .Must(_ => true)
            .WithErrorCodeAndMessage("Validation:TestCode");

        ValidationResult result = validator.Validate(new TestModel("valid"));

        result.IsValid.ShouldBeTrue();
    }

    // =========================================================================
    // WithPatternHint
    // =========================================================================

    [Fact]
    public void WithPatternHint_ChainedWithMatches_DoesNotBreakValidation()
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value)
            .Matches(@"^[A-Z]{2}$")
            .WithPatternHint("Validation:Hints:Alpha2Code");

        ValidationResult validResult = validator.Validate(new TestModel("BE"));
        validResult.IsValid.ShouldBeTrue();

        ValidationResult invalidResult = validator.Validate(new TestModel("invalid"));
        invalidResult.IsValid.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
