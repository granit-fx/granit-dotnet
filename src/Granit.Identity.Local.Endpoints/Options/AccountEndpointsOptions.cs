namespace Granit.Identity.Local.Endpoints.Options;

/// <summary>
/// Options for configuring account self-service endpoints.
/// </summary>
public sealed class AccountEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Local:Endpoints:Account";

    /// <summary>Route prefix for account self-service endpoints. Default: <c>"account"</c>.</summary>
    public string AccountRoutePrefix { get; set; } = "account";

    /// <summary>Route prefix for admin management endpoints. Default: <c>"admin"</c>.</summary>
    public string AdminRoutePrefix { get; set; } = "admin";

    /// <summary>
    /// Absolute frontend URL the external-login callback redirects the browser to after processing.
    /// When set, the callback responds with <c>302</c> and appends <c>status</c> (and, when profile
    /// completion is required, <c>token</c> + non-sensitive prefill, or otherwise the validated
    /// <c>returnUrl</c>). When <see langword="null"/> (the default), the callback returns the JSON
    /// <c>ExternalLoginCallbackResponse</c> instead — suitable for headless/test hosts. The
    /// <c>?mode=json</c> query forces the JSON response regardless of this setting.
    /// </summary>
    public string? ExternalLoginCallbackRedirectUrl { get; set; }

    /// <summary>OpenAPI tag for authentication endpoints (login, 2FA completion). Default: <c>"Account - Login"</c>.</summary>
    public string LoginTagName { get; set; } = "Account - Login";

    /// <summary>OpenAPI tag for registration endpoints (register, confirm email, resend confirmation). Default: <c>"Account - Registration"</c>.</summary>
    public string RegistrationTagName { get; set; } = "Account - Registration";

    /// <summary>OpenAPI tag for profile endpoints (get profile, update profile). Default: <c>"Account - Profile"</c>.</summary>
    public string ProfileTagName { get; set; } = "Account - Profile";

    /// <summary>OpenAPI tag for email-change endpoints (change email, confirm email change). Default: <c>"Account - Email Change"</c>.</summary>
    public string EmailChangeTagName { get; set; } = "Account - Email Change";

    /// <summary>OpenAPI tag for password endpoints (change, forgot, reset). Default: <c>"Account - Password"</c>.</summary>
#pragma warning disable GRSEC003 // OpenAPI tag label, not a secret
    public string PasswordTagName { get; set; } = "Account - Password";
#pragma warning restore GRSEC003

    /// <summary>OpenAPI tag for two-factor endpoints (enable, disable, recovery codes). Default: <c>"Account - Two-Factor"</c>.</summary>
    public string TwoFactorTagName { get; set; } = "Account - Two-Factor";

    /// <summary>OpenAPI tag for external-login endpoints (challenge, callback, list, unlink). Default: <c>"Account - External Logins"</c>.</summary>
    public string ExternalLoginsTagName { get; set; } = "Account - External Logins";

    /// <summary>OpenAPI tag for passkey endpoints (register, assertion, list, delete). Default: <c>"Account - Passkeys"</c>.</summary>
    public string PasskeysTagName { get; set; } = "Account - Passkeys";

    /// <summary>OpenAPI tag for account deletion endpoint. Default: <c>"Account - Deletion"</c>.</summary>
    public string DeletionTagName { get; set; } = "Account - Deletion";

    /// <summary>OpenAPI tag for session endpoints (heartbeat). Default: <c>"Account - Session"</c>.</summary>
    public string SessionTagName { get; set; } = "Account - Session";

    /// <summary>OpenAPI tag for the public config endpoint. Default: <c>"Account - Config"</c>.</summary>
    public string ConfigTagName { get; set; } = "Account - Config";

    /// <summary>
    /// OpenAPI tag shared by both impersonation endpoints:
    /// <c>POST /admin/users/{userId}/impersonate</c> (start) and
    /// <c>POST /account/session/back-to-impersonator</c> (end).
    /// Default: <c>"Impersonation"</c>.
    /// </summary>
    public string ImpersonationTagName { get; set; } = "Impersonation";
}
