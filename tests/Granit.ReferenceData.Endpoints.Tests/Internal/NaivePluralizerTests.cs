using Granit.ReferenceData.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests.Internal;

public sealed class NaivePluralizerTests
{
    [Theory]
    [InlineData("Country", "Countries")]
    [InlineData("Currency", "Currencies")]
    [InlineData("Category", "Categories")]
    [InlineData("Policy", "Policies")]
    public void Pluralize_ConsonantPlusY_ReplacesWithIes(string singular, string expected) =>
        NaivePluralizer.Pluralize(singular).ShouldBe(expected);

    [Theory]
    [InlineData("Survey", "Surveys")]
    [InlineData("Key", "Keys")]
    [InlineData("Day", "Days")]
    public void Pluralize_VowelPlusY_AppendsS(string singular, string expected) =>
        NaivePluralizer.Pluralize(singular).ShouldBe(expected);

    [Theory]
    [InlineData("Status", "Statuses")]
    [InlineData("Tax", "Taxes")]
    [InlineData("Address", "Addresses")]
    public void Pluralize_SibilantEnding_AppendsEs(string singular, string expected) =>
        NaivePluralizer.Pluralize(singular).ShouldBe(expected);

    [Theory]
    [InlineData("Blob", "Blobs")]
    [InlineData("Template", "Templates")]
    [InlineData("Product", "Products")]
    [InlineData("Tenant", "Tenants")]
    public void Pluralize_StandardEnding_AppendsS(string singular, string expected) =>
        NaivePluralizer.Pluralize(singular).ShouldBe(expected);

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Pluralize_NullOrEmpty_ReturnsSameValue(string? singular, string? expected) =>
        NaivePluralizer.Pluralize(singular!).ShouldBe(expected);
}
