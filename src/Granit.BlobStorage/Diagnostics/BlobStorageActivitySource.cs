using System.Diagnostics;

namespace Granit.BlobStorage.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.BlobStorage</c> distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class BlobStorageActivitySource
{
    /// <summary>The name of the <c>Granit.BlobStorage</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.BlobStorage";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
