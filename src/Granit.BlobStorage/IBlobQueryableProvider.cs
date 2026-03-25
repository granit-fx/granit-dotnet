using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage;

/// <summary>
/// Provides <see cref="IQueryable{T}"/> access to blob entities for use by
/// <c>Granit.QueryEngine</c> query endpoints.
/// </summary>
/// <remarks>
/// This is <b>not</b> a repository — it exposes raw queryables for the query engine.
/// All filtering, sorting, and pagination logic lives in <see cref="Granit.QueryEngine"/>.
/// The default no-op implementation returns empty queryables; the
/// <c>Granit.BlobStorage.EntityFrameworkCore</c> package replaces it with DbContext-backed sources.
/// </remarks>
public interface IBlobQueryableProvider
{
    /// <summary>Returns a queryable source for <see cref="BlobDescriptor"/> entities.</summary>
    IQueryable<BlobDescriptor> GetDescriptors();
}
