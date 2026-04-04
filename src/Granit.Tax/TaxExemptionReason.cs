namespace Granit.Tax;

/// <summary>Reason a transaction is tax-exempt.</summary>
public enum TaxExemptionReason
{
    /// <summary>No exemption — standard rate applies.</summary>
    None = 0,

    /// <summary>Intra-EU B2B reverse charge (Art. 196 VAT Directive).</summary>
    ReverseCharge = 1,

    /// <summary>Intra-community supply of goods.</summary>
    IntraCommunitarySupply = 2,

    /// <summary>Export to non-EU/non-UK country.</summary>
    Export = 3,

    /// <summary>Exempt goods or services (education, healthcare, etc.).</summary>
    ExemptGood = 4,

    /// <summary>Small business exemption (Kleinunternehmerregelung, etc.).</summary>
    SmallBusiness = 5,
}
