namespace Granit.OpenIddict;

/// <summary>
/// Well-known setting names for the OpenIddict module.
/// </summary>
/// <remarks>
/// These settings are registered via <c>OpenIddictSettingDefinitionProvider</c> (auto-discovered
/// by <c>GranitSettingsModule</c>) and can be overridden per-tenant at runtime via the
/// <c>Granit.Settings</c> admin API.
/// </remarks>
#pragma warning disable GRSEC003 // Setting name constants, not secrets
public static class OpenIddictSettingNames
{
    /// <summary>
    /// Access token lifetime (TimeSpan string). Default: <c>"01:00:00"</c> (1 hour).
    /// </summary>
    public const string AccessTokenLifetime = "OpenIddict.AccessTokenLifetime";

    /// <summary>
    /// Refresh token lifetime (TimeSpan string). Default: <c>"14.00:00:00"</c> (14 days).
    /// </summary>
    public const string RefreshTokenLifetime = "OpenIddict.RefreshTokenLifetime";

    /// <summary>
    /// Authorization code lifetime (TimeSpan string). Default: <c>"00:05:00"</c> (5 minutes).
    /// </summary>
    public const string AuthCodeLifetime = "OpenIddict.AuthCodeLifetime";

    /// <summary>
    /// Maximum failed login attempts before lockout. Default: <c>"5"</c>.
    /// </summary>
    public const string MaxLoginAttempts = "OpenIddict.MaxLoginAttempts";

    /// <summary>
    /// Idle session timeout in minutes. <c>"0"</c> disables the feature. Default: <c>"0"</c>.
    /// </summary>
    public const string IdleSessionTimeout = "OpenIddict.IdleSessionTimeout";
}
#pragma warning restore GRSEC003
