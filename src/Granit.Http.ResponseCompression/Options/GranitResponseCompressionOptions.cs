using System.IO.Compression;

namespace Granit.Http.ResponseCompression.Options;

/// <summary>
/// Configuration options for the Granit response compression module.
/// Bound from the <c>"ResponseCompression"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// HTTPS compression is enabled by default. BREACH/CRIME attacks are mitigated
/// by two defenses that together break the attack chain:
/// <list type="bullet">
///   <item><b>SameSite cookies</b> (Strict/Lax) — prevents an attacker-origin
///   from triggering authenticated cross-site requests the attack needs.</item>
///   <item><b>Per-request antiforgery tokens</b> (ASP.NET Core rotates the
///   request token on every response) — even if BREACH could measure body
///   sizes, the specific token it might extract is already invalidated by the
///   time the attack completes.</item>
/// </list>
/// </para>
/// <para>
/// <b>Do NOT enable compression on HTML responses that reflect attacker-
/// controlled input AND carry a non-rotating secret</b> (embedded session
/// identifier, JWT, derived API key, etc.). Granit is a JSON-first platform
/// where this combination is rare; if you serve HTML views with embedded
/// secrets beyond the rotating antiforgery token, disable compression on
/// those endpoints (<see cref="EnableForHttps"/> = false) or switch the
/// secret to a per-request rotating value.
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
