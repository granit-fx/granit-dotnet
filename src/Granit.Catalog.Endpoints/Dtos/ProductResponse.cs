using Granit.Catalog.Domain;
using Granit.Domain;

namespace Granit.Catalog.Endpoints.Dtos;

/// <summary>Catalog product entry.</summary>
public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    string Type,
    string Unit,
    string LifecycleStatus,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<ProductExternalMappingResponse> ExternalMappings)
{
    internal static ProductResponse FromEntity(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Type.ToString(),
        product.Unit,
        product.LifecycleStatus.ToString(),
        product.GetMetadata(),
        product.ExternalMappings.Select(ProductExternalMappingResponse.FromEntity).ToList());
}

/// <summary>Product external provider mapping (Stripe, Avalara, Odoo, ...).</summary>
public sealed record ProductExternalMappingResponse(
    Guid Id,
    string ProviderName,
    string ExternalId)
{
    internal static ProductExternalMappingResponse FromEntity(ProductExternalMapping mapping) =>
        new(mapping.Id, mapping.ProviderName, mapping.ExternalId);
}
