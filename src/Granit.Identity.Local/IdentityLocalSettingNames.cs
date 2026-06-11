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
    /// Controls whether new users can self-register — the master switch for account
    /// creation across both the local <c>POST /api/account/register</c> flow and the
    /// external-provider auto-registration / profile-completion flow. Boolean string
    /// (<c>"true"</c> / <c>"false"</c>). Default: <c>"false"</c> (disabled). When
    /// disabled, an external login still authenticates an <em>existing</em> account but
    /// never creates a new one.
    /// </summary>
    public const string AllowSelfRegistration = "Identity.Local.AllowSelfRegistration";

    /// <summary>
    /// Name of the role assigned to every newly self-registered user — both local and
    /// external — via the shared <c>UserRegisteredEto</c> handler. Default: <c>""</c>
    /// (empty = opt-in disabled, no role assigned). The role must already exist; if it
    /// does not, registration still succeeds but the user receives no role
    /// (secure-by-default, non-blocking).
    /// </summary>
    public const string DefaultUserRole = "Identity.Local.DefaultUserRole";
}
#pragma warning restore GRSEC003
