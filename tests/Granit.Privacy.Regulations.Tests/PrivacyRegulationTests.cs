using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests;

public sealed class PrivacyRegulationTests
{
    [Fact]
    public void StaticFields_ContainExpectedTier1Regulations()
    {
        PrivacyRegulation.EuGdpr.Value.ShouldBe("EU_GDPR");
        PrivacyRegulation.UkGdpr.Value.ShouldBe("UK_GDPR");
        PrivacyRegulation.BrLgpd.Value.ShouldBe("BR_LGPD");
        PrivacyRegulation.UsCcpa.Value.ShouldBe("US_CCPA");
        PrivacyRegulation.CaPipeda.Value.ShouldBe("CA_PIPEDA");
        PrivacyRegulation.CaQuebec25.Value.ShouldBe("CA_QUEBEC_25");
        PrivacyRegulation.ChNfadp.Value.ShouldBe("CH_NFADP");
    }

    [Fact]
    public void StaticFields_ContainExpectedTier2Regulations()
    {
        PrivacyRegulation.CnPipl.Value.ShouldBe("CN_PIPL");
        PrivacyRegulation.InDpdpa.Value.ShouldBe("IN_DPDPA");
        PrivacyRegulation.JpAppi.Value.ShouldBe("JP_APPI");
        PrivacyRegulation.KrPipa.Value.ShouldBe("KR_PIPA");
        PrivacyRegulation.AuPrivacyAct.Value.ShouldBe("AU_PRIVACY_ACT");
        PrivacyRegulation.ZaPopia.Value.ShouldBe("ZA_POPIA");
        PrivacyRegulation.ThPdpa.Value.ShouldBe("TH_PDPA");
    }

    [Fact]
    public void Create_ValidCode_ReturnsValueObject()
    {
        var custom = PrivacyRegulation.Create("SA_PDPL");

        custom.Value.ShouldBe("SA_PDPL");
    }

    [Fact]
    public void Create_NullOrWhiteSpace_Throws()
    {
        Should.Throw<ArgumentException>(() => PrivacyRegulation.Create(null!));
        Should.Throw<ArgumentException>(() => PrivacyRegulation.Create(""));
        Should.Throw<ArgumentException>(() => PrivacyRegulation.Create("   "));
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = PrivacyRegulation.Create("EU_GDPR");
        PrivacyRegulation b = PrivacyRegulation.EuGdpr;

        a.ShouldBe(b);
    }

    [Fact]
    public void ImplicitConversion_ToString()
    {
        string code = PrivacyRegulation.EuGdpr;

        code.ShouldBe("EU_GDPR");
    }

    [Fact]
    public void ImplicitConversion_FromString()
    {
        PrivacyRegulation regulation = "BR_LGPD";

        regulation.Value.ShouldBe("BR_LGPD");
    }
}
