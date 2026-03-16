namespace Granit.AI.VectorData.Options;

/// <summary>
/// Configuration options for the Granit AI vector data module.
/// </summary>
public sealed class VectorDataOptions
{
    /// <summary>
    /// The configuration section name (<c>AI:VectorData</c>).
    /// </summary>
    public const string SectionName = "AI:VectorData";

    /// <summary>
    /// The logical name of the embedding workspace.
    /// Used to resolve the correct <see cref="Granit.AI.IEmbeddingGeneratorFactory"/> instance.
    /// </summary>
    public string EmbeddingWorkspace { get; set; } = "default";

    /// <summary>
    /// The default maximum number of results returned by search operations.
    /// </summary>
    public int DefaultSearchLimit { get; set; } = 10;
}
