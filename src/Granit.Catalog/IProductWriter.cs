using Granit.Catalog.Domain;

namespace Granit.Catalog;

/// <summary>Persists product changes (command side of CQRS).</summary>
public interface IProductWriter
{
    /// <summary>Persists a new product.</summary>
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing product (lifecycle, fields, mappings).</summary>
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
}
