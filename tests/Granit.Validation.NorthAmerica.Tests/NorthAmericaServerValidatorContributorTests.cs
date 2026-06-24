using Granit.Validation.NorthAmerica.ServerValidation;
using Granit.Validation.ServerValidation;
using Shouldly;
using Xunit;

namespace Granit.Validation.NorthAmerica.Tests;

public sealed class NorthAmericaServerValidatorContributorTests
{
    private readonly List<IServerValidator> _validators;

    public NorthAmericaServerValidatorContributorTests()
    {
        _validators = new NorthAmericaServerValidatorContributor().GetValidators().ToList();
    }

    [Fact]
    public void GetValidators_Returns8Validators() =>
        _validators.Count.ShouldBe(8);

    [Fact]
    public void AllErrorCodes_AreUnique() =>
        _validators.Select(v => v.ErrorCode).Distinct().Count().ShouldBe(_validators.Count);

    [Theory]
    [InlineData("Validation:Format:UsSsn", "078051120", true)]
    [InlineData("Validation:Format:UsSsn", "000000000", false)]
    [InlineData("Validation:Format:UsZipCode", "10001", true)]
    [InlineData("Validation:Format:UsZipCode", "ABCDE", false)]
    [InlineData("Validation:Format:CanadianPostalCode", "K1A 0B1", true)]
    [InlineData("Validation:Format:CanadianPostalCode", "INVALID", false)]
    public void Validate_ReturnsExpectedResult(string errorCode, string? value, bool expected)
    {
        IServerValidator validator = _validators.Single(v => v.ErrorCode == errorCode);
        validator.Validate(value).ShouldBe(expected);
    }
}
