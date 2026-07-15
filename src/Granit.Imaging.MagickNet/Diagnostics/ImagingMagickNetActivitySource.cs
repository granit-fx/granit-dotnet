using System.Diagnostics;

namespace Granit.Imaging.MagickNet.Diagnostics;

/// <summary>OpenTelemetry activity source for Magick.NET image processing.</summary>
internal static class ImagingMagickNetActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Imaging.MagickNet";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string LoadOperation = "imaging.load";
    internal const string IdentifyOperation = "imaging.identify";
    internal const string EncodeOperation = "imaging.encode";

    internal const string TagSourceFormat = "imaging.source_format";
    internal const string TagOutputFormat = "imaging.output_format";
    internal const string TagWidth = "imaging.width";
    internal const string TagHeight = "imaging.height";
    internal const string TagInputBytes = "imaging.input_bytes";
}
