namespace Granit.Http.ApiDocumentation.Scalar.Options;

/// <summary>
/// UI-side OAuth2 client configuration for the Scalar Authorize popup. The flow's
/// endpoints and scopes come from the generation-side
/// <c>Http:ApiDocumentation:OAuth2</c> section (<c>Granit.Http.ApiDocumentation</c>);
/// this type only carries what the browser client needs.
/// </summary>
public sealed class ScalarOAuth2Options
{
    /// <summary>
    /// OAuth2 client ID — the <strong>public</strong> (frontend) client, not the backend confidential client.
    /// Must support PKCE (Authorization Code + S256).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>Enable PKCE (Proof Key for Code Exchange) with S256. Default: <c>true</c>.</summary>
    public bool EnablePkce { get; set; } = true;

    /// <summary>
    /// Absolute redirect URI passed to the Scalar OAuth2 flow. Workaround for
    /// <c>Scalar.AspNetCore</c> 2.12.40+ where the default redirect URI changed
    /// and breaks Authorization Code popups (scalar/scalar#8165, #8187): the
    /// popup re-opens same-origin but on a URL Scalar no longer recognises, so
    /// the auth code is never piped back and the user sees
    /// "Window was closed without granting authorization." Typically set to
    /// the absolute URL of the Scalar UI (e.g. <c>http://localhost:5000/scalar</c>).
    /// When <c>null</c>, Scalar's (currently broken) default is used — leave it
    /// unset only when you trust the upstream default again.
    /// </summary>
    public string? RedirectUri { get; set; }
}
