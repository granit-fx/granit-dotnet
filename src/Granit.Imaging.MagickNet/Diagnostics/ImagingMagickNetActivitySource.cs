using System.Diagnostics;

namespace Granit.Imaging.MagickNet.Diagnostics;

/// <summary>OpenTelemetry activity source for Magick.NET image processing.</summary>
internal static class ImagingMagickNetActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Imaging.MagickNet";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
