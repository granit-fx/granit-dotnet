namespace Granit.Diagnostics.Endpoints.Workspaces;

/// <summary>
/// Feature name constants for the diagnostics module (per ADR-057).
/// Mirrors the <c>XxxPermissions</c> pattern: hosts compose workspaces
/// with <c>section.Feature(DiagnosticsFeatures.Monitoring)</c> rather
/// than typing the string by hand, so a rename is a compile-time fix
/// instead of a silent drop at boot.
/// </summary>
public static class DiagnosticsFeatures
{
    /// <summary>Aggregated health dashboard — pairs with <c>/diagnostics</c> on the React shell.</summary>
    public const string Monitoring = "diagnostics.monitoring";
}
