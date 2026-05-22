using Granit.DataExchange.Import.Validation;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Validation;

public sealed class RowValidationResultTests
{
    [Fact]
    public void IsValid_NoErrors_ReturnsTrue()
    {
        var sut = new RowValidationResult();

        sut.IsValid.ShouldBeTrue();
        sut.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void IsValid_WithErrors_ReturnsFalse()
    {
        var sut = new RowValidationResult
        {
            Errors = [new RowFieldError("Name", "Validation:NotEmpty", "Name is required.")]
        };

        sut.IsValid.ShouldBeFalse();
        sut.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void IsValid_MultipleErrors_ReturnsFalse()
    {
        var sut = new RowValidationResult
        {
            Errors =
            [
                new RowFieldError("Name", "ERR1", "msg1"),
                new RowFieldError("Email", "ERR2", "msg2")
            ]
        };

        sut.IsValid.ShouldBeFalse();
        sut.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void Errors_DefaultsToEmptyList()
    {
        var sut = new RowValidationResult();

        sut.Errors.ShouldNotBeNull();
        sut.Errors.ShouldBeEmpty();
    }
}
