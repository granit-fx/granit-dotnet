namespace Granit.Tax;

/// <summary>Source system that performed the tax ID validation.</summary>
public enum TaxIdValidationSource
{
    /// <summary>Offline format/checksum validation only.</summary>
    Offline = 0,

    /// <summary>Offline validation accepted as fallback — pending online verification.</summary>
    OfflinePending = 1,

    /// <summary>EU VIES (VAT Information Exchange System).</summary>
    Vies = 2,

    /// <summary>UK HMRC API.</summary>
    HmrcApi = 3,

    /// <summary>Stripe Tax IDs API.</summary>
    StripeTax = 4,
}
