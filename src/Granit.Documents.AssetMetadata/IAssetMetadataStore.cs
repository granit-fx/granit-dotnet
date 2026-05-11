using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Domain;

namespace Granit.Documents.AssetMetadata;

/// <summary>
/// Storage abstraction for <see cref="DocumentAssetMetadata"/> aggregates. The
/// EF Core implementation lives in
/// <c>Granit.Documents.AssetMetadata.EntityFrameworkCore</c>; tests substitute
/// an in-memory implementation.
/// </summary>
public interface IAssetMetadataStore
{
    /// <summary>Persists a freshly-created row (typically <see cref="AssetMetadataStatus.Pending"/>).</summary>
    Task AddAsync(DocumentAssetMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>Persists state changes on an existing row.</summary>
    Task UpdateAsync(DocumentAssetMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>Returns the row for the given version, or <c>null</c>.</summary>
    Task<DocumentAssetMetadata?> GetByVersionAsync(
        Guid documentVersionId, CancellationToken cancellationToken = default);

    /// <summary>Returns every row attached to the document (across versions).</summary>
    Task<IReadOnlyList<DocumentAssetMetadata>> ListForDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Removes every row for the given document — invoked on permanent-delete cascade.</summary>
    Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
