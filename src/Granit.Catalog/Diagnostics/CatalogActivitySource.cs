using System.Diagnostics;

namespace Granit.Catalog.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Catalog distributed tracing.
/// </summary>
internal static class CatalogActivitySource
{
    internal const string Name = "Granit.Catalog";

    internal static readonly ActivitySource Source = new(Name);

    internal const string CreateProduct = "catalog.product.create";
    internal const string PublishProduct = "catalog.product.publish";
    internal const string ArchiveProduct = "catalog.product.archive";
    internal const string AddExternalMapping = "catalog.product.external_mapping.add";
    internal const string RemoveExternalMapping = "catalog.product.external_mapping.remove";
}
