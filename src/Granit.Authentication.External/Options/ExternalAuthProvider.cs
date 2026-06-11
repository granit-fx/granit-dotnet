namespace Granit.Authentication.External.Options;

/// <summary>
/// Configuration for a single external authentication provider instance.
/// </summary>
/// <remarks>
/// One entry maps to one registered ASP.NET Core authentication scheme. The
/// <see cref="SchemeName"/> is the contract consumers resolve against (the OAuth challenge
/// targets a scheme with that exact name).
/// </remarks>
public sealed class ExternalAuthProvider
{
    /// <summary>
    /// The provider kind, selecting which handler package wires it: <c>Google</c>, <c>Microsoft</c>,
    /// <c>Apple</c>, <c>GitHub</c>, <c>Facebook</c>, or <c>Oidc</c> (generic OpenID Connect).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The authentication scheme name. Defaults to <see cref="Type"/> when unset; set it
    /// explicitly to run several instances of the same kind (e.g. two <c>Oidc</c> providers).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>The OAuth/OIDC client (application) identifier.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth/OIDC client secret. NEVER commit a real value — inject via user-secrets,
    /// environment variables, or Vault.
    /// </summary>
#pragma warning disable GRSEC003 // Bound from secret config, not a hardcoded secret
    public string ClientSecret { get; set; } = string.Empty;
#pragma warning restore GRSEC003

    /// <summary>Scopes to request. When empty, the provider's package default applies.</summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>The OIDC authority (issuer) URL. Required for the generic <c>Oidc</c> provider.</summary>
    public string? Authority { get; set; }

    /// <summary>Overrides the handler's default callback path (e.g. <c>/signin-google</c>).</summary>
    public string? CallbackPath { get; set; }

    /// <summary>
    /// Optional human-friendly label for the provider (e.g. shown on a "Continue with …" button).
    /// When unset, a default is derived from <see cref="Type"/> (or <see cref="SchemeName"/> for
    /// the generic <c>Oidc</c> provider, which can have several instances).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Provider-specific extra settings keyed by name (e.g. Apple <c>TeamId</c>/<c>KeyId</c>/
    /// <c>PrivateKey</c>, a GitHub Enterprise base URL). Interpreted by each provider package.
    /// </summary>
    public Dictionary<string, string?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The resolved scheme name: <see cref="Name"/> when set, otherwise <see cref="Type"/>.</summary>
    public string SchemeName => string.IsNullOrWhiteSpace(Name) ? Type : Name!;

    /// <summary>
    /// The label to display for this provider: <see cref="DisplayName"/> when set, otherwise the
    /// provider <see cref="Type"/> (already display-ready for Google/Microsoft/Apple/GitHub/Facebook),
    /// or the <see cref="SchemeName"/> for the generic <c>Oidc</c> provider.
    /// </summary>
    public string ResolvedDisplayName => string.IsNullOrWhiteSpace(DisplayName)
        ? (string.Equals(Type, "Oidc", StringComparison.OrdinalIgnoreCase) ? SchemeName : Type)
        : DisplayName!;
}
