namespace Granit.AI.VectorData;

/// <summary>
/// Factory for creating tenant-scoped vector collections.
/// </summary>
public interface IVectorCollectionFactory
{
    /// <summary>
    /// Gets or creates a vector collection for the specified name.
    /// The collection is automatically scoped to the current tenant.
    /// </summary>
    /// <typeparam name="TRecord">The record type stored in the collection.</typeparam>
    /// <param name="collectionName">The logical name of the vector collection.</param>
    /// <returns>A tenant-scoped vector collection instance.</returns>
    IVectorCollection<TRecord> GetCollection<TRecord>(string collectionName) where TRecord : class;
}
