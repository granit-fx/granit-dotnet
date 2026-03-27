namespace Granit.Identity.Local.Options;

/// <summary>
/// Configuration options for WebAuthn/FIDO2 passkey authentication.
/// </summary>
public sealed class GranitPasskeyOptions
{
    /// <summary>Configuration section name for binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "Identity:Passkeys";

    /// <summary>
    /// Gets or sets the Relying Party ID (domain). Required.
    /// </summary>
    /// <example><c>"example.com"</c></example>
    public string ServerDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeout for authenticator operations.
    /// </summary>
    public TimeSpan AuthenticatorTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the challenge size in bytes.
    /// </summary>
    public int ChallengeSize { get; set; } = 32;
}
