using Granit.Tax.Internal;
using Shouldly;
using Xunit;

namespace Granit.Tax.Internal.Tests.Internal;

public sealed class EuCountriesTests
{
    // ======== IsEuMember ========

    [Theory]
    [InlineData("BE")]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("GR")]
    [InlineData("SE")]
    public void IsEuMember_EuCountry_ShouldReturnTrue(string countryCode) =>
        EuCountries.IsEuMember(countryCode).ShouldBeTrue();

    [Theory]
    [InlineData("US")]
    [InlineData("CH")]
    [InlineData("GB")]
    [InlineData("JP")]
    public void IsEuMember_NonEuCountry_ShouldReturnFalse(string countryCode) =>
        EuCountries.IsEuMember(countryCode).ShouldBeFalse();

    [Fact]
    public void IsEuMember_GreeceEl_ShouldReturnTrue() =>
        EuCountries.IsEuMember("EL").ShouldBeTrue();

    [Theory]
    [InlineData("be")]
    [InlineData("Be")]
    [InlineData("bE")]
    public void IsEuMember_CaseInsensitive_ShouldReturnTrue(string countryCode) =>
        EuCountries.IsEuMember(countryCode).ShouldBeTrue();

    // ======== MemberStates ========

    [Fact]
    public void MemberStates_ShouldContain27Countries() =>
        EuCountries.MemberStates.Count.ShouldBe(27);
}
