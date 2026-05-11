using System;
using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.Renditions.Office.Options;

/// <summary>Configuration options for <c>Granit.Documents.Renditions.Office</c>.</summary>
public sealed class OfficeRenditionOptions
{
    /// <summary>Configuration section key (<c>"Documents:Renditions:Office"</c>).</summary>
    public const string SectionName = "Documents:Renditions:Office";

    /// <summary>
    /// Path to the LibreOffice headless binary. Default <c>"soffice"</c>; hosts that
    /// install LibreOffice outside the PATH (or on Windows / macOS) override with the
    /// absolute path (e.g. <c>"/usr/bin/soffice"</c>,
    /// <c>"C:\Program Files\LibreOffice\program\soffice.com"</c>).
    /// </summary>
    [Required]
    public string SofficeBinary { get; set; } = "soffice";

    /// <summary>
    /// Maximum number of concurrent <c>soffice</c> invocations. Default <c>1</c> —
    /// LibreOffice headless is not safe to run concurrently against the same user
    /// profile. Raising this requires a per-worker user profile (configure
    /// <see cref="UserProfileDirectoryTemplate"/> with a unique template).
    /// </summary>
    [Range(1, 16)]
    public int MaxConcurrentConversions { get; set; } = 1;

    /// <summary>
    /// Hard timeout for a single conversion. Default 60 seconds — typical office
    /// thumbnails complete in 2–4 seconds; the cap protects the host against runaway
    /// invocations on adversarial documents.
    /// </summary>
    public TimeSpan ConversionTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Optional override for the per-invocation <c>-env:UserInstallation</c> URL.
    /// When <c>null</c> (default), each invocation gets a fresh temp directory under
    /// <c>{temp}/granit-soffice-{guid}</c>. Useful only when the host wants to pin a
    /// shared profile or use a tmpfs path for performance.
    /// </summary>
    public string? UserProfileDirectoryTemplate { get; set; }
}
