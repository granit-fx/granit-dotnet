using System.Diagnostics;

namespace Granit.Indexing.Embeddings.Diagnostics;

/// <summary>
/// <see cref="System.Diagnostics.ActivitySource"/> for the embeddings module. Registered
/// at module init via <c>GranitActivitySourceRegistry</c> so OTel collectors pick it up.
/// </summary>
internal static class EmbeddingsActivitySource
{
    public const string Name = "Granit.Indexing.Embeddings";

    public static readonly ActivitySource Instance = new(Name);
}
