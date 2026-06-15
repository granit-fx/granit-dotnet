using Granit.Privacy.Regulations.Internal;
using Granit.Privacy.Regulations.Profiles.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests.Internal;

public sealed class CompositeRegulationProfileMergerTests
{
    private static PrivacyRegulationProfile Gdpr { get; } = BuildRegistry().GetProfile(PrivacyRegulation.EuGdpr)!;
    private static PrivacyRegulationProfile Ccpa { get; } = BuildRegistry().GetProfile(PrivacyRegulation.UsCcpa)!;
    private static PrivacyRegulationProfile Quebec25 { get; } = BuildRegistry().GetProfile(PrivacyRegulation.CaQuebec25)!;
    private static PrivacyRegulationProfile Pipeda { get; } = BuildRegistry().GetProfile(PrivacyRegulation.CaPipeda)!;
    private static PrivacyRegulationProfile Lgpd { get; } = BuildRegistry().GetProfile(PrivacyRegulation.BrLgpd)!;

    [Fact]
    public void Merge_SingleProfile_ReturnsUnchanged()
    {
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr]);

        result.ShouldBeSameAs(Gdpr);
    }

    [Fact]
    public void Merge_EmptyList_Throws() =>
        Should.Throw<ArgumentException>(() => CompositeRegulationProfileMerger.Merge([]));

    [Fact]
    public void Merge_GdprPlusCcpa_OptInWins()
    {
        // GDPR = OptIn (strictest), CCPA = OptOut
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Ccpa]);

        result.ConsentModel.ShouldBe(ConsentModel.OptIn);
        result.CookieConsentModel.ShouldBe(ConsentModel.OptIn);
    }

    [Fact]
    public void Merge_GdprPlusCcpa_MinimumSarDeadline()
    {
        // GDPR = 30 days, CCPA = 45 days → merged = 30
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Ccpa]);

        result.SubjectAccessRequestDays.ShouldBe(
            Math.Min(Gdpr.SubjectAccessRequestDays, Ccpa.SubjectAccessRequestDays));
    }

    [Fact]
    public void Merge_GdprPlusCcpa_MinimumBreachDeadline()
    {
        // GDPR = 72h, CCPA = null (not specified) → merged = 72h
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Ccpa]);

        result.BreachNotifyAuthorityHours.ShouldBe(Gdpr.BreachNotifyAuthorityHours);
    }

    [Fact]
    public void Merge_LegalBasesAreUnioned()
    {
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Lgpd]);

        foreach (LegalBasis basis in Gdpr.AvailableLegalBases)
        {
            result.AvailableLegalBases.ShouldContain(basis);
        }
        foreach (LegalBasis basis in Lgpd.AvailableLegalBases)
        {
            result.AvailableLegalBases.ShouldContain(basis);
        }
    }

    [Fact]
    public void Merge_ExportFormatsAreUnioned()
    {
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Lgpd]);

        foreach (string fmt in Gdpr.RequiredExportFormats)
        {
            result.RequiredExportFormats.ShouldContain(fmt);
        }
        foreach (string fmt in Lgpd.RequiredExportFormats)
        {
            result.RequiredExportFormats.ShouldContain(fmt);
        }
    }

    [Fact]
    public void Merge_CompositeRegulationCodeIsJoined()
    {
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Ccpa]);

        result.Regulation.Value.ShouldBe("EU_GDPR+US_CCPA");
    }

    [Fact]
    public void Merge_OrderIndependent()
    {
        PrivacyRegulationProfile ab = CompositeRegulationProfileMerger.Merge([Gdpr, Ccpa]);
        PrivacyRegulationProfile ba = CompositeRegulationProfileMerger.Merge([Ccpa, Gdpr]);

        // Merged fields that should be order-independent
        ab.ConsentModel.ShouldBe(ba.ConsentModel);
        ab.SubjectAccessRequestDays.ShouldBe(ba.SubjectAccessRequestDays);
        ab.DataLocalizationRequired.ShouldBe(ba.DataLocalizationRequired);
        ab.RequiresDpoOrRepresentative.ShouldBe(ba.RequiresDpoOrRepresentative);
        ab.MinimumConsentAge.ShouldBe(ba.MinimumConsentAge);
    }

    [Fact]
    public void Merge_QuebecPlusPipeda_BothOptIn_NoChange()
    {
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Quebec25, Pipeda]);

        result.ConsentModel.ShouldBe(ConsentModel.OptIn);
    }

    [Fact]
    public void Merge_DataLocalizationRequired_TrueIfAnyRequires()
    {
        // GDPR = false, CN_PIPL = true (data localization required in China)
        // We use GDPR + any profile that has DataLocalizationRequired = true
        // Build a synthetic profile with data localization
        PrivacyRegulationProfile withLocalization = Gdpr with
        {
            Regulation = PrivacyRegulation.Create("TEST_LOC"),
            DataLocalizationRequired = true,
        };

        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, withLocalization]);

        result.DataLocalizationRequired.ShouldBeTrue();
    }

    [Fact]
    public void Merge_ThreeRegulations_Works()
    {
        PrivacyRegulationProfile result = CompositeRegulationProfileMerger.Merge([Gdpr, Ccpa, Lgpd]);

        result.Regulation.Value.ShouldBe("EU_GDPR+US_CCPA+BR_LGPD");
        result.ConsentModel.ShouldBe(ConsentModel.OptIn);
    }

    private static RegulationProfileRegistry BuildRegistry()
    {
        var context = new RegulationProfileContext();
        new BuiltInRegulationProfileProvider().Define(context);
        return new RegulationProfileRegistry(context.Build());
    }
}
