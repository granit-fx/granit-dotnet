using System.ComponentModel.DataAnnotations;

namespace Granit.IO.Options;

/// <summary>
/// Options for <see cref="ITempFileFactory"/> and the temp-file janitor.
/// </summary>
public sealed class TempFileOptions
{
    /// <summary>Configuration section name: <c>Granit:IO:TempFiles</c>.</summary>
    public const string SectionName = "Granit:IO:TempFiles";

    /// <summary>Default root directory: <c>{Path.GetTempPath()}/granit</c>.</summary>
    public static string DefaultRootDirectory { get; } = Path.Combine(Path.GetTempPath(), "granit");

    /// <summary>
    /// Root directory under which temp files are created.
    /// Defaults to <see cref="DefaultRootDirectory"/> when <c>null</c>.
    /// </summary>
    public string? RootDirectory { get; set; }

    /// <summary>
    /// When <c>true</c> (default), set <c>0600</c> on files and <c>0700</c> on
    /// directories on POSIX platforms. No effect on Windows.
    /// </summary>
    public bool ChmodOwnerOnly { get; set; } = true;

    /// <summary>
    /// Maximum lifetime of a temp file before the janitor purges it.
    /// Defaults to <c>30 minutes</c>.
    /// </summary>
    public TimeSpan MaxLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum size (bytes) writeable to a single temp file. Defaults to <c>100 MiB</c>.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// When <c>true</c> (default), files are placed under a per-tenant sub-directory.
    /// </summary>
    public bool TenantPartition { get; set; } = true;

    /// <summary>
    /// Interval between janitor sweeps. Defaults to <c>5 minutes</c>.
    /// </summary>
    public TimeSpan JanitorInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When <c>true</c> (default), the <c>TempFileJanitor</c> hosted service runs
    /// in the background. Set to <c>false</c> in tests or short-lived hosts.
    /// </summary>
    public bool RunJanitor { get; set; } = true;

    /// <summary>Effective root directory, resolving the default when <see cref="RootDirectory"/> is null/empty.</summary>
    public string EffectiveRootDirectory =>
        string.IsNullOrWhiteSpace(RootDirectory) ? DefaultRootDirectory : RootDirectory;
}
