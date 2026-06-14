using Granit.Validation.Finance.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Finance.Tests;

public sealed class SepaCreditorIdentifierAlgorithmTests
{
    [Theory]
    [InlineData("BE46ZZZ000000000")]
    [InlineData("FR20ZZZ123456")]
    [InlineData("be46zzz000000000")]                     // Lowercase
    [InlineData("BE 46 ZZZ 000000000")]                  // With spaces
    public void IsValid_ValidSci_ReturnsTrue(string sci) => SepaCreditorIdentifierAlgorithm.IsValid(sci).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BE47ZZZ000000000")]                     // Wrong check digits
    [InlineData("BE92")]                                 // Too short
    [InlineData("1234ZZZ000000000")]                     // Country code not alpha
    [InlineData("BEAAZZZ000000000")]                     // Check not digits
    public void IsValid_InvalidSci_ReturnsFalse(string? sci) => SepaCreditorIdentifierAlgorithm.IsValid(sci).ShouldBeFalse();
}
