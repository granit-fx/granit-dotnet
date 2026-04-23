using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage;

/// <summary>
/// Write-only persistence abstraction for <see cref="BlobDescriptor"/> records.
/// </summary>
/// <remarks>
/// Implemented by <c>Granit.BlobStorage.EntityFrameworkCore</c>.
/// No hard-delete method exists by design (GDPR/ISO 27001: audit rows are retained for 3 years).
/// </remarks>
public interface IBlobDescriptorWriter
{
    /// <summary>Persists a newly created <see cref="BlobDescriptor"/>.</summary>
    Task SaveAsync(BlobDescriptor descriptor, CancellationToken cancellationToken = default);

    /// <summary>Persists state changes on an existing <see cref="BlobDescriptor"/>.</summary>
    Task UpdateAsync(BlobDescriptor descriptor, CancellationToken cancellationToken = default);
}
