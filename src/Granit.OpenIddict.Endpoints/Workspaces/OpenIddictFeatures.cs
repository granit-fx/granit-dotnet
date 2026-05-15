namespace Granit.OpenIddict.Endpoints.Workspaces;

/// <summary>
/// Feature name constants for the OpenIddict (OIDC) module (per ADR-057).
/// </summary>
public static class OpenIddictFeatures
{
    /// <summary>OIDC applications list — paired with <c>/oidc/applications</c>.</summary>
    public const string Applications = "openiddict.applications";

    /// <summary>OIDC scopes list — paired with <c>/oidc/scopes</c>.</summary>
    public const string Scopes = "openiddict.scopes";

    /// <summary>OIDC authorizations list — paired with <c>/oidc/authorizations</c>.</summary>
    public const string Authorizations = "openiddict.authorizations";
}
