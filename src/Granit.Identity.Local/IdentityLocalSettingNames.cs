namespace Granit.Identity.Local;

/// <summary>
/// Well-known setting names for the Identity.Local module.
/// </summary>
/// <remarks>
/// These settings are registered via <c>IdentityLocalSettingDefinitionProvider</c> (auto-discovered
/// by <c>GranitSettingsModule</c>) and can be overridden per-tenant at runtime via the
/// <c>Granit.Settings</c> admin API.
/// </remarks>
#pragma warning disable GRSEC003 // Setting name constants, not secrets
public static class IdentityLocalSettingNames
{
    /// <summary>
    /// Controls whether new users can self-register via <c>POST /api/account/register</c>.
    /// Boolean string (<c>"true"</c> / <c>"false"</c>). Default: <c>"false"</c> (disabled).
    /// </summary>
    public const string AllowSelfRegistration = "Identity.Local.AllowSelfRegistration";
}
#pragma warning restore GRSEC003
