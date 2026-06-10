namespace Granit.Identity.Local.Options;

/// <summary>
/// Configuration options for WebAuthn/FIDO2 passkey authentication.
/// </summary>
public sealed class GranitPasskeyOptions
{
    /// <summary>Configuration section name for binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "Identity:Local:Passkey";

    /// <summary>
    /// Gets or sets the Relying Party ID (domain). Required.
    /// </summary>
    /// <example><c>"example.com"</c></example>
    public string ServerDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Relying Party display name shown to users by their authenticator.
    /// </summary>
    public string ServerName { get; set; } = "Granit";

    /// <summary>
    /// Gets or sets the set of allowed origins (full URLs) the WebAuthn ceremony will accept
    /// in <c>clientDataJSON.origin</c>. Required — leaving this empty fails startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FIDO2 binds every assertion to a single origin. The receiver must reject any
    /// assertion whose <c>clientDataJSON.origin</c> does not match. Without this list,
    /// an attacker who hosts a malicious site under a different origin could relay an
    /// assertion crafted there and get the receiver to accept it.
    /// </para>
    /// <para>
    /// Typical content: <c>["https://app.example.com"]</c>. Multiple origins (mobile
    /// app + web app) are supported.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the timeout for authenticator operations.
    /// </summary>
    public TimeSpan AuthenticatorTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the challenge size in bytes.
    /// </summary>
    public int ChallengeSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets how long a server-issued challenge stays usable in the
    /// challenge cache before the receiver rejects the corresponding ceremony.
    /// </summary>
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
