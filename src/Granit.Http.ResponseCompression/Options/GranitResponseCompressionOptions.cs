using System.IO.Compression;

namespace Granit.Http.ResponseCompression.Options;

/// <summary>
/// Configuration options for the Granit response compression module.
/// Bound from the <c>"ResponseCompression"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// HTTPS compression is enabled by default. BREACH/CRIME attacks are mitigated
/// by Granit's antiforgery token enforcement, CORS restrictions, and SameSite cookies.
/// </para>
/// <para>
/// <c>text/event-stream</c> (SSE) is always excluded — this is a safety invariant
/// and cannot be overridden via configuration.
/// </para>
/// </remarks>
public sealed class GranitResponseCompressionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ResponseCompression";

    /// <summary>
    /// Whether to compress responses served over HTTPS.
    /// Default: <c>true</c> (BREACH mitigated by antiforgery tokens).
    /// </summary>
    public bool EnableForHttps { get; set; } = true;

    /// <summary>
    /// Enable Brotli compression (primary, best ratio). Default: <c>true</c>.
    /// </summary>
    public bool EnableBrotli { get; set; } = true;

    /// <summary>
    /// Enable gzip compression (fallback for older clients). Default: <c>true</c>.
    /// </summary>
    public bool EnableGzip { get; set; } = true;

    /// <summary>
    /// Brotli compression level. Default: <see cref="CompressionLevel.Fastest"/>
    /// (balanced speed/ratio for API responses).
    /// </summary>
    public CompressionLevel BrotliLevel { get; set; } = CompressionLevel.Fastest;

    /// <summary>
    /// Gzip compression level. Default: <see cref="CompressionLevel.Fastest"/>.
    /// </summary>
    public CompressionLevel GzipLevel { get; set; } = CompressionLevel.Fastest;
}
