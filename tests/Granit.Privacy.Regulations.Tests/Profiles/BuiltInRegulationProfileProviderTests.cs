using Granit.Privacy.Regulations.Profiles.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests.Profiles;

public sealed class BuiltInRegulationProfileProviderTests
{
    private readonly RegulationProfileRegistry _registry;

    public BuiltInRegulationProfileProviderTests()
    {
        RegulationProfileContext context = new();
        new BuiltInRegulationProfileProvider().Define(context);
        _registry = new RegulationProfileRegistry(context.Build());
    }

    [Fact]
    public void Registry_Contains14BuiltInProfiles() =>
        _registry.GetAll().Count.ShouldBe(14);

    [Theory]
    [InlineData("EU_GDPR", "EU", 30, 72)]
    [InlineData("UK_GDPR", "GB", 30, 72)]
    [InlineData("BR_LGPD", "BR", 15, null)]
    [InlineData("US_CCPA", "US-CA", 45, null)]
    [InlineData("CA_PIPEDA", "CA", 30, null)]
    [InlineData("CN_PIPL", "CN", 30, 24)]
    [InlineData("IN_DPDPA", "IN", 30, null)]
    [InlineData("KR_PIPA", "KR", 10, 72)]
    [InlineData("AU_PRIVACY_ACT", "AU", 30, 72)]
    [InlineData("TH_PDPA", "TH", 30, 72)]
    public void Profile_HasCorrectBaseValues(string code, string country, int sarDays, int? breachHours)
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.Create(code));

        profile.ShouldNotBeNull();
        profile.JurisdictionCode.ShouldBe(country);
        profile.SubjectAccessRequestDays.ShouldBe(sarDays);
        profile.BreachNotifyAuthorityHours.ShouldBe(breachHours);
    }

    [Fact]
    public void EuGdpr_HasOptInConsentModel()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.EuGdpr);

        profile.ShouldNotBeNull();
        profile.ConsentModel.ShouldBe(ConsentModel.OptIn);
        profile.CookieConsentModel.ShouldBe(ConsentModel.OptIn);
        profile.HonorGlobalPrivacyControl.ShouldBeFalse();
    }

    [Fact]
    public void UsCcpa_HasOptOutConsentModel_WithGpc()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.UsCcpa);

        profile.ShouldNotBeNull();
        profile.ConsentModel.ShouldBe(ConsentModel.OptOut);
        profile.CookieConsentModel.ShouldBe(ConsentModel.OptOut);
        profile.HonorGlobalPrivacyControl.ShouldBeTrue();
    }

    [Fact]
    public void BrLgpd_Has10LegalBases()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.BrLgpd);

        profile.ShouldNotBeNull();
        profile.AvailableLegalBases.Count.ShouldBe(10);
        profile.AvailableLegalBases.ShouldContain(LegalBasis.CreditProtection);
        profile.AvailableLegalBases.ShouldContain(LegalBasis.HealthProtection);
    }

    [Fact]
    public void EuGdpr_Has6LegalBases()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.EuGdpr);

        profile.ShouldNotBeNull();
        profile.AvailableLegalBases.Count.ShouldBe(6);
    }

    [Fact]
    public void InDpdpa_RequiresParentalIdentityVerification()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.InDpdpa);

        profile.ShouldNotBeNull();
        profile.MinimumConsentAge.ShouldBe(18);
        profile.RequiresParentalIdentityVerification.ShouldBeTrue();
    }

    [Fact]
    public void CnPipl_RequiresDataLocalization()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.CnPipl);

        profile.ShouldNotBeNull();
        profile.DataLocalizationRequired.ShouldBeTrue();
        profile.RequiresCrossBorderAssessment.ShouldBeTrue();
        profile.TransferMechanisms.Count.ShouldBe(3);
    }

    [Fact]
    public void UnregisteredRegulation_ReturnsNull()
    {
        PrivacyRegulationProfile? profile = _registry.GetProfile(PrivacyRegulation.Create("XX_UNKNOWN"));

        profile.ShouldBeNull();
    }
}
