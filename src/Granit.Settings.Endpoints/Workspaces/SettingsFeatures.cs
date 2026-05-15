namespace Granit.Settings.Endpoints.Workspaces;

/// <summary>Feature name constants for the settings module (per ADR-057).</summary>
public static class SettingsFeatures
{
    /// <summary>Global settings — paired with <c>/settings/global</c>.</summary>
    public const string Global = "settings.global";

    /// <summary>Tenant settings — paired with <c>/settings/tenant</c>.</summary>
    public const string Tenant = "settings.tenant";
}
