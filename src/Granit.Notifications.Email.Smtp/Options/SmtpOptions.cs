using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.Email.Smtp.Options;

/// <summary>SMTP server connection options.</summary>
public sealed class SmtpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:Email:Smtp";

    /// <summary>SMTP server hostname.</summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP server port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Whether to use SSL/TLS.</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>Default sender email address. Falls back to <see cref="Username"/> if not set.</summary>
    public string? DefaultSenderEmail { get; set; }

    /// <summary>Default sender display name.</summary>
    public string? DefaultSenderName { get; set; }

    /// <summary>SMTP authentication username.</summary>
    public string? Username { get; set; }

    /// <summary>SMTP authentication password.</summary>
    public string? Password { get; set; }

    /// <summary>Connection and send timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
