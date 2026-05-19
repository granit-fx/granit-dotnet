using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests;

public sealed class LegalBasisTests
{
    [Fact]
    public void GdprBases_HasSixBases()
    {
        LegalBasis[] gdprBases =
        [
            LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
            LegalBasis.VitalInterest, LegalBasis.PublicInterest, LegalBasis.LegitimateInterest,
        ];

        gdprBases.Length.ShouldBe(6);
    }

    [Fact]
    public void LgpdBases_HasFourAdditionalBases()
    {
        LegalBasis[] lgpdExtras =
        [
            LegalBasis.CreditProtection, LegalBasis.HealthProtection,
            LegalBasis.ResearchByStudyBodies, LegalBasis.LifeProtection,
        ];

        lgpdExtras.Length.ShouldBe(4);
        lgpdExtras.ShouldAllBe(b => !string.IsNullOrWhiteSpace(b.Value));
    }

    [Fact]
    public void Create_CustomBasis_Works()
    {
        var custom = LegalBasis.Create("EMERGENCY_DECREE");

        custom.Value.ShouldBe("EMERGENCY_DECREE");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = LegalBasis.Create("CONSENT");
        LegalBasis b = LegalBasis.Consent;

        a.ShouldBe(b);
    }
}
