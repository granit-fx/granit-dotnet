using Granit.Tax.Internal.Internal;
using Shouldly;
using Xunit;

namespace Granit.Tax.Internal.Tests.Internal;

public sealed class EuVatRuleEngineTests
{
    // ======== Export (buyer outside EU) ========

    [Fact]
    public void Classify_BuyerOutsideEu_ShouldReturnExport()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "US", buyerHasValidVat: false, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.Export);
        result.Exemption.ShouldBe(TaxExemptionReason.Export);
        result.RateCountryCode.ShouldBe("BE");
        result.IsBusinessToBusiness.ShouldBeFalse();
    }

    [Fact]
    public void Classify_BuyerOutsideEuWithVat_ShouldReturnExportB2B()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("FR", "CH", buyerHasValidVat: true, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.Export);
        result.Exemption.ShouldBe(TaxExemptionReason.Export);
        result.IsBusinessToBusiness.ShouldBeTrue();
        result.RateCountryCode.ShouldBe("FR");
    }

    // ======== Domestic sale (same country) ========

    [Fact]
    public void Classify_SameCountry_ShouldReturnDomesticSale()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "BE", buyerHasValidVat: false, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.DomesticSale);
        result.Exemption.ShouldBe(TaxExemptionReason.None);
        result.RateCountryCode.ShouldBe("BE");
    }

    [Fact]
    public void Classify_SameCountryWithVat_ShouldReturnDomesticB2B()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("DE", "DE", buyerHasValidVat: true, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.DomesticSale);
        result.IsBusinessToBusiness.ShouldBeTrue();
        result.RateCountryCode.ShouldBe("DE");
    }

    // ======== Greece normalization (GR / EL) ========

    [Fact]
    public void Classify_SellerElBuyerGr_ShouldReturnDomesticSale()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("EL", "GR", buyerHasValidVat: false, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.DomesticSale);
        result.Exemption.ShouldBe(TaxExemptionReason.None);
        result.RateCountryCode.ShouldBe("EL");
    }

    [Fact]
    public void Classify_SellerGrBuyerEl_ShouldReturnDomesticSale()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("GR", "EL", buyerHasValidVat: false, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.DomesticSale);
        result.RateCountryCode.ShouldBe("GR");
    }

    // ======== Intra-community B2B reverse charge ========

    [Fact]
    public void Classify_CrossBorderEuB2B_ShouldReturnReverseCharge()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "DE", buyerHasValidVat: true, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.IntraCommunityB2B);
        result.IsBusinessToBusiness.ShouldBeTrue();
        result.Exemption.ShouldBe(TaxExemptionReason.ReverseCharge);
        result.RateCountryCode.ShouldBe("DE");
    }

    // ======== Intra-community B2C with OSS ========

    [Fact]
    public void Classify_CrossBorderB2CWithOssNullCountries_ShouldUseBuyerCountryRate()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "FR", buyerHasValidVat: false, ossEnabled: true, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.IntraCommunityB2C);
        result.IsBusinessToBusiness.ShouldBeFalse();
        result.Exemption.ShouldBe(TaxExemptionReason.None);
        result.RateCountryCode.ShouldBe("FR");
    }

    [Fact]
    public void Classify_CrossBorderB2CWithOssEmptyCountries_ShouldUseBuyerCountryRate()
    {
        HashSet<string> empty = [];
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "IT", buyerHasValidVat: false, ossEnabled: true, ossCountries: empty);

        result.TransactionType.ShouldBe(TransactionType.IntraCommunityB2C);
        result.RateCountryCode.ShouldBe("IT");
    }

    [Fact]
    public void Classify_CrossBorderB2CWithOssBuyerIncluded_ShouldUseBuyerCountryRate()
    {
        HashSet<string> countries = ["FR", "DE", "IT"];
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "DE", buyerHasValidVat: false, ossEnabled: true, ossCountries: countries);

        result.TransactionType.ShouldBe(TransactionType.IntraCommunityB2C);
        result.RateCountryCode.ShouldBe("DE");
    }

    [Fact]
    public void Classify_CrossBorderB2CWithOssBuyerExcluded_ShouldUseSellerCountryRate()
    {
        HashSet<string> countries = ["FR", "IT"];
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "DE", buyerHasValidVat: false, ossEnabled: true, ossCountries: countries);

        result.TransactionType.ShouldBe(TransactionType.IntraCommunityB2C);
        result.RateCountryCode.ShouldBe("BE");
    }

    // ======== Intra-community B2C without OSS ========

    [Fact]
    public void Classify_CrossBorderB2CWithoutOss_ShouldUseSellerCountryRate()
    {
        TaxCalculationContext result = EuVatRuleEngine.Classify("BE", "FR", buyerHasValidVat: false, ossEnabled: false, ossCountries: null);

        result.TransactionType.ShouldBe(TransactionType.IntraCommunityB2C);
        result.IsBusinessToBusiness.ShouldBeFalse();
        result.Exemption.ShouldBe(TaxExemptionReason.None);
        result.RateCountryCode.ShouldBe("BE");
    }
}
