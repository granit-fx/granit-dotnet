using FluentValidation;
using Granit.Validation.OpenApi;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class PatternHintValidatorTests
{
    [Fact]
    public void HintKey_ReturnsProvidedKey()
    {
        PatternHintValidator<TestModel, string> validator = new("Validation:Hint:TestHint");

        validator.HintKey.ShouldBe("Validation:Hint:TestHint");
    }

    [Fact]
    public void Name_ReturnsPatternHintValidator()
    {
        PatternHintValidator<TestModel, string> validator = new("Validation:Hint:TestHint");

        validator.Name.ShouldBe("PatternHintValidator");
    }

    [Fact]
    public void IsValid_AlwaysReturnsTrue()
    {
        PatternHintValidator<TestModel, string> validator = new("Validation:Hint:TestHint");
        ValidationContext<TestModel> context = new(new TestModel("test"));

        bool result = validator.IsValid(context, "anything");

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_NullValue_ReturnsTrue()
    {
        PatternHintValidator<TestModel, string?> validator = new("Validation:Hint:TestHint");
        ValidationContext<TestModel> context = new(new TestModel(null));

        bool result = validator.IsValid(context, null);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_NullHintKey_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new PatternHintValidator<TestModel, string>(null!));
    }

    [Fact]
    public void ImplementsIPatternHintProvider()
    {
        PatternHintValidator<TestModel, string> validator = new("key");

        validator.ShouldBeAssignableTo<IPatternHintProvider>();
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
