namespace Granit.Persistence.EntityFrameworkCore.Conventions;

/// <summary>
/// Test-only gate for the native EF Core convention engine (#3158, overhaul Phase 2).
/// While the parallel implementation is validated against the golden-model baselines,
/// the legacy <c>ApplyGranitConventionsCore</c> pipeline stays the production path;
/// the flip (#3159) removes this switch.
/// </summary>
internal static class GranitNativeConventions
{
    /// <summary>AppContext switch name enabling the native engine on GranitDbContext.</summary>
    public const string SwitchName = "Granit.Persistence.NativeConventions";

    /// <summary><c>true</c> when the native convention engine is enabled.</summary>
    public static bool Enabled =>
        AppContext.TryGetSwitch(SwitchName, out bool enabled) && enabled;
}
