using System.Diagnostics;

namespace Granit.Documents.Renditions.Diagnostics;

/// <summary>Central <see cref="ActivitySource"/> for Granit.Documents.Renditions distributed tracing.</summary>
internal static class RenditionsActivitySource
{
    /// <summary>The name of the renditions <see cref="ActivitySource"/>.</summary>
    public const string Name = "Granit.Documents.Renditions";

    /// <summary>Singleton <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(Name);

    /// <summary>Span name covering the entire pipeline run (provider chain).</summary>
    public const string PipelineExecute = "renditions.pipeline.execute";

    /// <summary>Span name for a single provider hop within a pipeline.</summary>
    public const string ProviderGenerate = "renditions.provider.generate";

    /// <summary>Span name for the on-demand fallback path.</summary>
    public const string OnDemandServe = "renditions.on_demand.serve";
}
