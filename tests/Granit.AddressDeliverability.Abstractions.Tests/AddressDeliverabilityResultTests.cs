using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.AddressDeliverability.Abstractions.Tests;

public sealed class AddressDeliverabilityResultTests
{
    [Fact]
    public void Result_DefaultsOptionalFieldsToNull()
    {
        var result = new AddressDeliverabilityResult(AddressDeliverabilityOutcome.Verified);

        result.Outcome.ShouldBe(AddressDeliverabilityOutcome.Verified);
        result.Standardized.ShouldBeNull();
        result.Flags.ShouldBeNull();
        result.ProviderMatchCode.ShouldBeNull();
    }

    [Fact]
    public void Result_CarriesStandardizedAddressFlagsAndMatchCode()
    {
        var standardized = Address.Create("Rue de la Loi 16", "Brussels", "1000", "BE");

        var result = new AddressDeliverabilityResult(
            AddressDeliverabilityOutcome.Corrected, standardized, ["residential"], "AABBCC");

        result.Outcome.ShouldBe(AddressDeliverabilityOutcome.Corrected);
        result.Standardized.ShouldBe(standardized);
        result.Flags.ShouldNotBeNull().ShouldContain("residential");
        result.ProviderMatchCode.ShouldBe("AABBCC");
    }
}
