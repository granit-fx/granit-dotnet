using System.Diagnostics;

namespace Granit.Settings.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Settings distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class SettingsActivitySource
{
    /// <summary>The name of the Granit.Settings <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Settings";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────
    internal const string GetValue = "settings.get-value";
    internal const string SetValue = "settings.set-value";
    internal const string DeleteValue = "settings.delete-value";
}
