using Granit.Validation.ServerValidation;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class DelegatingServerValidatorTests
{
    [Fact]
    public void Validate_DelegatesToFunc()
    {
        var validator = new DelegatingServerValidator(
            "Granit:Validation:Test",
            value => value == "valid");

        validator.ErrorCode.ShouldBe("Granit:Validation:Test");
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
}
