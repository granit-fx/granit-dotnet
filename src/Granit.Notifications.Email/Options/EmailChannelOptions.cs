namespace Granit.Notifications.Email.Options;

/// <summary>Configuration for the email notification channel.</summary>
public sealed class EmailChannelOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:Email";

    /// <summary>Provider key for Keyed Services resolution (e.g. "Smtp", "SendGrid").</summary>
    public string Provider { get; set; } = "Smtp";

    /// <summary>Default sender email address (e.g. "noreply@example.com").</summary>
    public string DefaultSenderEmail { get; set; } = string.Empty;

    /// <summary>Default sender display name (e.g. "My Application").</summary>
    public string DefaultSenderName { get; set; } = string.Empty;

    /// <summary>
    /// URL for the notification preferences page. Used for the <c>List-Unsubscribe</c>
    /// header and the footer link in the email layout.
    /// If <c>null</c>, falls back to <c>{AppGlobalContextOptions.BaseUrl}/notifications/preferences</c>.
    /// </summary>
    public string? UnsubscribeUrl { get; set; }
}
