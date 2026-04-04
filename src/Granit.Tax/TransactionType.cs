namespace Granit.Tax;

/// <summary>Classification of a transaction for tax purposes.</summary>
public enum TransactionType
{
    /// <summary>Seller and buyer in the same country.</summary>
    DomesticSale = 0,

    /// <summary>Intra-EU B2B sale (buyer has valid VAT — reverse charge eligible).</summary>
    IntraCommunityB2B = 1,

    /// <summary>Intra-EU B2C sale (consumer — OSS or seller country rate).</summary>
    IntraCommunityB2C = 2,

    /// <summary>Export to non-EU country (0% tax).</summary>
    Export = 3,
}
