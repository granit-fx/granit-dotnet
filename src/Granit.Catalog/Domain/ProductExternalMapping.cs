using Granit.Domain;

namespace Granit.Catalog.Domain;

/// <summary>
/// Maps a <see cref="Product"/> to an external provider identifier
/// (e.g., Stripe <c>prod_xxx</c>, Avalara tax code, Odoo <c>product.product</c> id).
/// </summary>
/// <remarks>
/// One product may have several mappings — typically one per external system. Pairs
/// (<see cref="ProviderName"/>, <see cref="ExternalId"/>) need not be unique within
/// a product (e.g., the same Stripe id may map to multiple regional products);
/// uniqueness, when required, is enforced at the application layer or via DB index.
/// </remarks>
public sealed class ProductExternalMapping : Entity
{
    private ProductExternalMapping() { }

    /// <summary>Creates a new external mapping.</summary>
    public static ProductExternalMapping Create(Guid id, string providerName, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return new ProductExternalMapping
        {
            Id = id,
            ProviderName = providerName,
            ExternalId = externalId,
        };
    }

    /// <summary>External provider name (e.g., <c>"stripe"</c>, <c>"avalara"</c>, <c>"odoo"</c>).</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>External identifier in the provider system (e.g., <c>"prod_1234"</c>).</summary>
    public string ExternalId { get; private set; } = string.Empty;
}
