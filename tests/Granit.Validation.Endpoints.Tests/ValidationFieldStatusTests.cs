using Granit.Validation.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationFieldStatusTests
{
    [Fact]
    public void Valid_HasExpectedValue()
    {
        ValidationFieldStatus status = ValidationFieldStatus.Valid;

        ((int)status).ShouldBe(0);
    }

    [Fact]
    public void Invalid_HasExpectedValue()
    {
        ValidationFieldStatus status = ValidationFieldStatus.Invalid;

        ((int)status).ShouldBe(1);
    }

    [Fact]
    public void ValidatorNotFound_HasExpectedValue()
    {
        ValidationFieldStatus status = ValidationFieldStatus.ValidatorNotFound;

        ((int)status).ShouldBe(2);
    }

    [Fact]
    public void AllValues_AreDefined()
    {
        string[] names = Enum.GetNames<ValidationFieldStatus>();

        names.Length.ShouldBe(3);
        names.ShouldContain("Valid");
        names.ShouldContain("Invalid");
        names.ShouldContain("ValidatorNotFound");
    }
}
