namespace Granit.Catalog.Endpoints.Dtos;

/// <summary>Request to create a new product in Draft status.</summary>
public sealed record ProductCreateRequest(
    string Sku,
    string Name,
    string Type,
    string Unit,
    string? Description = null);

/// <summary>Request to update editable fields of a Draft product.</summary>
public sealed record ProductUpdateRequest(
    string Name,
    string? Description,
    string Unit);

/// <summary>Request to replace all extra properties of a product (any lifecycle status).</summary>
/// <remarks>
/// Maps to <see cref="Granit.Domain.IHasMetadata.MetadataJson"/> via
/// <c>Product.ReplaceMetadata</c>. MUST NOT contain PII (audit logs and exports
/// surface this content).
/// </remarks>
public sealed record UpdateProductMetadataRequest(
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Request to add an external provider mapping to a product.</summary>
public sealed record AddProductExternalMappingRequest(
    string ProviderName,
    string ExternalId);
