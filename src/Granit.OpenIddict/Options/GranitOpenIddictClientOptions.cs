namespace Granit.OpenIddict.Options;

/// <summary>
/// Configuration options for external login providers.
/// </summary>
/// <remarks>
/// Bind from <c>appsettings.json</c> section <c>"OpenIddict:Client"</c>.
/// </remarks>
public sealed class GranitOpenIddictClientOptions
{
    /// <summary>Configuration section name for binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "OpenIddict:Client";

    /// <summary>
    /// Gets or sets a value indicating whether to auto-register users from external providers.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, a new <c>LocalIdentity</c> is created on first login
    /// via external provider. When <see langword="false"/>, returns 403 if no existing account.
    /// </remarks>
    public bool AutoRegisterExternalUsers { get; set; } = true;

    /// <summary>
    /// Gets or sets the external login providers to configure.
    /// </summary>
    public ExternalProviderOptions[] Providers { get; set; } = [];
}

/// <summary>
/// Configuration for a single external login provider.
/// </summary>
public sealed class ExternalProviderOptions
{
    /// <summary>Gets or sets the provider name ("Google", "Microsoft", "GitHub").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the OAuth client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OAuth client secret.
    /// </summary>
    /// <remarks>
    /// <b>Security:</b> NEVER store in <c>appsettings.json</c> in production.
    /// Use <c>Granit.Vault</c> secret injection or environment variables.
    /// </remarks>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the OAuth scopes to request.</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];
}
