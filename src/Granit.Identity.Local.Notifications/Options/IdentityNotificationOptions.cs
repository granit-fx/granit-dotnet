using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Local.Notifications.Options;

/// <summary>
/// Configuration for identity notification URL generation.
/// Bound to <c>Identity:Notifications</c> configuration section.
/// </summary>
public sealed class IdentityNotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Notifications";

    /// <summary>Base URL of the frontend application (e.g., <c>https://app.example.com</c>).</summary>
    [Required]
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>Relative path for the password reset page.</summary>
#pragma warning disable GRSEC003 // Path segment, not a secret
    public string ResetPasswordPath { get; set; } = "reset-password";
#pragma warning restore GRSEC003

    /// <summary>Relative path for the email confirmation page.</summary>
    public string ConfirmEmailPath { get; set; } = "confirm-email";

    /// <summary>Relative path for the email change confirmation page.</summary>
    public string ChangeEmailPath { get; set; } = "confirm-email-change";

    /// <summary>Builds a password reset URL with the given user ID and token.</summary>
    public string BuildResetPasswordUrl(string userId, string token) =>
        BuildResetPasswordUrl(FrontendBaseUrl, userId, token);

    /// <summary>Builds a password reset URL using an explicit base URL (tenant-aware).</summary>
    public string BuildResetPasswordUrl(string baseUrl, string userId, string token) =>
        $"{baseUrl.TrimEnd('/')}/{ResetPasswordPath}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";

    /// <summary>Builds an email confirmation URL with the given user ID and token.</summary>
    public string BuildConfirmEmailUrl(string userId, string token) =>
        BuildConfirmEmailUrl(FrontendBaseUrl, userId, token);

    /// <summary>Builds an email confirmation URL using an explicit base URL (tenant-aware).</summary>
    public string BuildConfirmEmailUrl(string baseUrl, string userId, string token) =>
        $"{baseUrl.TrimEnd('/')}/{ConfirmEmailPath}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";

    /// <summary>Builds an email change confirmation URL with the given user ID, new email, and token.</summary>
    public string BuildChangeEmailUrl(string userId, string newEmail, string token) =>
        BuildChangeEmailUrl(FrontendBaseUrl, userId, newEmail, token);

    /// <summary>Builds an email change confirmation URL using an explicit base URL (tenant-aware).</summary>
    public string BuildChangeEmailUrl(string baseUrl, string userId, string newEmail, string token) =>
        $"{baseUrl.TrimEnd('/')}/{ChangeEmailPath}?userId={Uri.EscapeDataString(userId)}&newEmail={Uri.EscapeDataString(newEmail)}&token={Uri.EscapeDataString(token)}";
}
