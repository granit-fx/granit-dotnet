using Granit.Validation.ServerValidation;
using Granit.Validation.UnitedKingdom.ServerValidation;
using Shouldly;
using Xunit;

namespace Granit.Validation.UnitedKingdom.Tests;

public sealed class UnitedKingdomServerValidatorContributorTests
{
    private readonly List<IServerValidator> _validators;

    public UnitedKingdomServerValidatorContributorTests()
    {
        _validators = new UnitedKingdomServerValidatorContributor().GetValidators().ToList();
    }

    [Fact]
    public void GetValidators_Returns7Validators() =>
        _validators.Count.ShouldBe(7);

    [Fact]
    public void AllErrorCodes_AreUnique() =>
        _validators.Select(v => v.ErrorCode).Distinct().Count().ShouldBe(_validators.Count);

    [Theory]
    [InlineData("Granit:Validation:InvalidUkPostcode", "SW1A 1AA", true)]
    [InlineData("Granit:Validation:InvalidUkPostcode", "INVALID", false)]
    [InlineData("Granit:Validation:InvalidUkSortCode", "20-00-00", true)]
    [InlineData("Granit:Validation:InvalidUkSortCode", "ABCDEF", false)]
    public void Validate_ReturnsExpectedResult(string errorCode, string? value, bool expected)
    {
        IServerValidator validator = _validators.Single(v => v.ErrorCode == errorCode);
        validator.Validate(value).ShouldBe(expected);
    }
}
