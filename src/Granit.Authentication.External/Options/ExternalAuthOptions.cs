namespace Granit.Authentication.External.Options;

/// <summary>
/// Options for external/social authentication providers, bound from the
/// <c>Authentication:External</c> configuration section.
/// </summary>
public sealed class ExternalAuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication:External";

    /// <summary>
    /// Whether a first external login with no matching local account auto-provisions one.
    /// When <see langword="false"/>, the callback returns 403 if no account exists.
    /// </summary>
    public bool AutoRegisterExternalUsers { get; set; } = true;

    /// <summary>The configured external provider instances.</summary>
    public IList<ExternalAuthProvider> Providers { get; set; } = [];
}
