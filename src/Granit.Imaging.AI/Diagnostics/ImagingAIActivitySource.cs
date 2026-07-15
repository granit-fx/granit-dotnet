using System.Diagnostics;

namespace Granit.Imaging.AI.Diagnostics;

/// <summary>OpenTelemetry activity source for AI-powered image analysis.</summary>
internal static class ImagingAIActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Imaging.AI";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string AnalyzeOperation = "imaging.ai.analyze";
    internal const string ExtractTextOperation = "imaging.ai.extract_text";

    internal const string TagContentType = "imaging.ai.content_type";
    internal const string TagImageSizeBytes = "imaging.ai.image_size_bytes";
    internal const string TagWorkspace = "imaging.ai.workspace";
}
