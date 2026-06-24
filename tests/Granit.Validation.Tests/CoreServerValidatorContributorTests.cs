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
    public void GetValidators_Returns18Validators() =>
        // IBAN / BIC/SWIFT / SEPA Creditor Identifier moved to Granit.Validation.Finance;
        // AbsoluteUri server-validator added to mirror the AbsoluteUri() rule.
        _validators.Count.ShouldBe(18);

    [Fact]
    public void AllErrorCodes_AreUnique() =>
        _validators.Select(v => v.ErrorCode).Distinct().Count().ShouldBe(_validators.Count);

    [Theory]
    [InlineData("Validation:Format:Email", "user@example.com", true)]
    [InlineData("Validation:Format:Email", "not-an-email", false)]
    [InlineData("Validation:Format:Slug", "my-slug-123", true)]
    [InlineData("Validation:Format:Slug", "UPPER CASE", false)]
    [InlineData("Validation:Format:Url", "https://example.com", true)]
    [InlineData("Validation:Format:Url", "not a url", false)]
    [InlineData("Validation:Format:UrlHttps", "https://example.com", true)]
    [InlineData("Validation:Format:UrlHttps", "http://example.com", false)]
    [InlineData("Validation:Format:Ipv4Address", "192.168.1.1", true)]
    [InlineData("Validation:Format:Ipv4Address", "999.999.999.999", false)]
    [InlineData("Validation:Format:Iso3166Alpha2", "BE", true)]
    [InlineData("Validation:Format:Iso3166Alpha2", "123", false)]
    [InlineData("Validation:Format:Uuid", "550e8400-e29b-41d4-a716-446655440000", true)]
    [InlineData("Validation:Format:Uuid", "not-a-uuid", false)]
    public void Validate_ReturnsExpectedResult(string errorCode, string? value, bool expected)
    {
        IServerValidator validator = _validators.Single(v => v.ErrorCode == errorCode);
        validator.Validate(value).ShouldBe(expected);
    }
}
