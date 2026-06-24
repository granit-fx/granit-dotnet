using Granit.Validation.Europe.ServerValidation;
using Granit.Validation.ServerValidation;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class EuropeServerValidatorContributorTests
{
    private readonly List<IServerValidator> _validators;

    public EuropeServerValidatorContributorTests()
    {
        _validators = new EuropeServerValidatorContributor().GetValidators().ToList();
    }

    [Fact]
    public void GetValidators_Returns34Validators() =>
        _validators.Count.ShouldBe(34);

    [Fact]
    public void AllErrorCodes_AreUnique() =>
        _validators.Select(v => v.ErrorCode).Distinct().Count().ShouldBe(_validators.Count);

    [Theory]
    [InlineData("Validation:Format:FrenchSiren", "732829320", true)]
    [InlineData("Validation:Format:FrenchSiren", "732829321", false)]
    [InlineData("Validation:Format:BelgianBce", "0202239951", true)]
    [InlineData("Validation:Format:BelgianBce", "0000000000", false)]
    [InlineData("Validation:Format:BelgianNiss", "85073003328", true)]
    [InlineData("Validation:Format:BelgianNiss", "00000000000", false)]
    [InlineData("Validation:Format:FrenchPostalCode", "75001", true)]
    [InlineData("Validation:Format:FrenchPostalCode", "00001", false)]
    [InlineData("Validation:Format:BelgianPostalCode", "1000", true)]
    [InlineData("Validation:Format:BelgianPostalCode", "0001", false)]
    [InlineData("Validation:Format:GermanSteuerId", "86095742719", true)]
    [InlineData("Validation:Format:DutchBsn", "111222333", true)]
    [InlineData("Validation:Format:SpanishNif", "12345678Z", true)]
    [InlineData("Validation:Format:SpanishNif", "12345678A", false)]
    public void Validate_ReturnsExpectedResult(string errorCode, string? value, bool expected)
    {
        IServerValidator validator = _validators.Single(v => v.ErrorCode == errorCode);
        validator.Validate(value).ShouldBe(expected);
    }
}
