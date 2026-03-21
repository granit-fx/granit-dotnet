using Granit.Validation.ServerValidation;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class DelegatingServerValidatorTests
{
    [Fact]
    public void ErrorCode_ReturnsProvidedCode()
    {
        DelegatingServerValidator validator = new("Granit:Validation:Test", _ => true);

        validator.ErrorCode.ShouldBe("Granit:Validation:Test");
    }

    [Fact]
    public void Validate_DelegatesToProvidedFunc()
    {
        DelegatingServerValidator validator = new(
            "Granit:Validation:Test",
            value => value == "valid");

        validator.Validate("valid").ShouldBeTrue();
        validator.Validate("invalid").ShouldBeFalse();
        validator.Validate(null).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NullErrorCode_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new DelegatingServerValidator(null!, _ => true));
    }

    [Fact]
    public void ImplementsIServerValidator()
    {
        DelegatingServerValidator validator = new("Granit:Validation:Test", _ => true);

        validator.ShouldBeAssignableTo<IServerValidator>();
    }
}
