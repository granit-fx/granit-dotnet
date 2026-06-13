using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Notifications.Options;

/// <summary>
/// Configuration for user-session notification URL generation.
/// Bound to the <c>Identity:Notifications:Sessions</c> configuration section.
/// </summary>
public sealed class IdentitySessionNotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Notifications:Sessions";

    /// <summary>Base URL of the frontend application (e.g., <c>https://app.example.com</c>).</summary>
    [Required]
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Relative path of the account-security page the "secure your account" CTA links to.
    /// </summary>
#pragma warning disable GRSEC003 // Path segment, not a secret
    public string SecurityPagePath { get; set; } = "account/security";
#pragma warning restore GRSEC003

    /// <summary>Builds the "secure your account" CTA URL.</summary>
    public string BuildSecurityUrl() => BuildSecurityUrl(FrontendBaseUrl);

    /// <summary>Builds the "secure your account" CTA URL using an explicit base URL (tenant-aware).</summary>
    public string BuildSecurityUrl(string baseUrl) =>
        $"{baseUrl.TrimEnd('/')}/{SecurityPagePath.TrimStart('/')}";
}
