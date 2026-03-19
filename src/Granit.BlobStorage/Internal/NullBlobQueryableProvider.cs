using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage.Internal;

/// <summary>
/// Default no-op implementation of <see cref="IBlobQueryableProvider"/>.
/// Returns empty queryables. Replaced by <c>EfBlobQueryableProvider</c> when
/// <c>Granit.BlobStorage.EntityFrameworkCore</c> is loaded.
/// </summary>
internal sealed class NullBlobQueryableProvider : IBlobQueryableProvider
{
    public IQueryable<BlobDescriptor> GetDescriptors() =>
        Enumerable.Empty<BlobDescriptor>().AsQueryable();
}
