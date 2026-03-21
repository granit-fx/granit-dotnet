using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class IbanAlgorithmTests
{
    [Theory]
    [InlineData("BE68539007547034")]
    [InlineData("BE68 5390 0754 7034")]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("DE89370400440532013000")]
    [InlineData("GB29NWBK60161331926819")]
    [InlineData("NL91ABNA0417164300")]
    [InlineData("be68539007547034")]                     // lowercase
    public void IsValid_ValidIban_ReturnsTrue(string iban) => IbanAlgorithm.IsValid(iban).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BE6853900754703")]                      // Too short (14 chars)
    [InlineData("BE68539007547035")]                     // Wrong check digit
    [InlineData("BE68539007547034ABCDEFGHIJKLMNOP1234")] // Too long (>34 chars)
    [InlineData("BE6853900754703!")]                     // Special character
    public void IsValid_InvalidIban_ReturnsFalse(string? iban) => IbanAlgorithm.IsValid(iban).ShouldBeFalse();
}
