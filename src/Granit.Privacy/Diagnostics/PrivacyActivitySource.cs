using System.Diagnostics;

namespace Granit.Privacy.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Privacy distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class PrivacyActivitySource
{
    /// <summary>The name of the Granit.Privacy <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Privacy";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string ExportExecute = "privacy.export.execute";
    internal const string ArchiveAssemble = "privacy.export.archive.assemble";
    internal const string DeletionExecute = "privacy.deletion.execute";
    internal const string DeletionDefer = "privacy.deletion.defer";
    internal const string DeletionCancel = "privacy.deletion.cancel";
}
