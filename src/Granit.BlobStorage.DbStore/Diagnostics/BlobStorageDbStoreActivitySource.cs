using System.Diagnostics;

namespace Granit.BlobStorage.DbStore.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.BlobStorage.DbStore distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class BlobStorageDbStoreActivitySource
{
    /// <summary>The name of the Granit.BlobStorage.DbStore <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.BlobStorage.DbStore";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Delete = "blobstorage.delete";
    internal const string GetSize = "blobstorage.get-size";
    internal const string PartialStream = "blobstorage.partial-stream";
    internal const string Save = "blobstorage.save";
    internal const string Read = "blobstorage.read";

    // ──── Tag names ────

    internal const string TagObjectKey = "blobstorage.object_key";
    internal const string TagContentType = "blobstorage.content_type";
}
