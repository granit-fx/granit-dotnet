using Granit.Catalog.Domain;
using Granit.Workflow.Domain;

namespace Granit.Catalog;

/// <summary>Reads catalog products (query side of CQRS).</summary>
public interface IProductReader
{
    /// <summary>Returns a product by ID (any lifecycle status).</summary>
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns a product by SKU (any lifecycle status).</summary>
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all products with the given <paramref name="status"/>.
    /// Pass <see cref="WorkflowLifecycleStatus.Published"/> to list catalog items
    /// available for use by Subscriptions and Metering.
    /// </summary>
    Task<IReadOnlyList<Product>> GetByStatusAsync(
        WorkflowLifecycleStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a product by external provider mapping.</summary>
    Task<Product?> GetByExternalIdAsync(
        string providerName,
        string externalId,
        CancellationToken cancellationToken = default);
}
