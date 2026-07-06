using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests;

public sealed class LegalBasisTests
{
    [Fact]
    public void GdprBases_ExposeCanonicalArticle6Codes()
    {
        // Pins the six GDPR Art. 6(1) lawful-basis codes exposed by the SUT — these strings are
        // a compliance contract (persisted + surfaced in DSR exports), so drift must break here.
        LegalBasis.Consent.Value.ShouldBe("CONSENT");
        LegalBasis.Contract.Value.ShouldBe("CONTRACT");
        LegalBasis.LegalObligation.Value.ShouldBe("LEGAL_OBLIGATION");
        LegalBasis.VitalInterest.Value.ShouldBe("VITAL_INTEREST");
        LegalBasis.PublicInterest.Value.ShouldBe("PUBLIC_INTEREST");
        LegalBasis.LegitimateInterest.Value.ShouldBe("LEGITIMATE_INTEREST");
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
