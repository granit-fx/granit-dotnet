using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.SendGrid.Options;

/// <summary>SendGrid connection options.</summary>
public sealed class SendGridEmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:SendGrid";

    /// <summary>SendGrid API key. Required.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Default sender email address (must be verified in SendGrid).</summary>
    [Required]
    public string DefaultSenderEmail { get; set; } = string.Empty;

    /// <summary>Default sender display name.</summary>
    public string DefaultSenderName { get; set; } = string.Empty;

    /// <summary>SendGrid API base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.sendgrid.com/v3";

    /// <summary>Send timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
