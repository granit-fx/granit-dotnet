using Granit.Validation.ServerValidation;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class CoreServerValidatorContributorTests
{
    private readonly List<IServerValidator> _validators;

    public CoreServerValidatorContributorTests()
    {
        _validators = new CoreServerValidatorContributor().GetValidators().ToList();
    }

    [Fact]
    public void GetValidators_Returns19Validators() =>
        _validators.Count.ShouldBe(19);

    [Fact]
    public void AllErrorCodes_AreUnique() =>
        _validators.Select(v => v.ErrorCode).Distinct().Count().ShouldBe(_validators.Count);

    [Theory]
    [InlineData("Validation:InvalidIban", "BE68539007547034", true)]
    [InlineData("Validation:InvalidIban", "INVALID", false)]
    [InlineData("Validation:InvalidEmail", "user@example.com", true)]
    [InlineData("Validation:InvalidEmail", "not-an-email", false)]
    [InlineData("Validation:InvalidBicSwift", "GEBABEBB", true)]
    [InlineData("Validation:InvalidBicSwift", "X", false)]
    [InlineData("Validation:InvalidSlug", "my-slug-123", true)]
    [InlineData("Validation:InvalidSlug", "UPPER CASE", false)]
    [InlineData("Validation:InvalidUrl", "https://example.com", true)]
    [InlineData("Validation:InvalidUrl", "not a url", false)]
    [InlineData("Validation:InvalidIpv4Address", "192.168.1.1", true)]
    [InlineData("Validation:InvalidIpv4Address", "999.999.999.999", false)]
    [InlineData("Validation:InvalidIso3166Alpha2", "BE", true)]
    [InlineData("Validation:InvalidIso3166Alpha2", "123", false)]
    [InlineData("Validation:InvalidUuid", "550e8400-e29b-41d4-a716-446655440000", true)]
    [InlineData("Validation:InvalidUuid", "not-a-uuid", false)]
    public void Validate_ReturnsExpectedResult(string errorCode, string? value, bool expected)
    {
        IServerValidator validator = _validators.Single(v => v.ErrorCode == errorCode);
        validator.Validate(value).ShouldBe(expected);
    }
}
