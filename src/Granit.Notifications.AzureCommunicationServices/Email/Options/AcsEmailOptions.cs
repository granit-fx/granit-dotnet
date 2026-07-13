using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.AzureCommunicationServices.Email.Options;

/// <summary>Azure Communication Services email connection options.</summary>
public sealed class AcsEmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:AzureCommunicationServices:Email";

    /// <summary>
    /// ACS connection string. When provided, takes precedence over <see cref="Endpoint"/>.
    /// Either <see cref="ConnectionString"/> or <see cref="Endpoint"/> must be set.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// ACS endpoint URI (e.g. "https://my-acs.communication.azure.com").
    /// Used with <see cref="Azure.Identity.DefaultAzureCredential"/> when
    /// <see cref="ConnectionString"/> is not provided.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Default sender email address (must be verified in ACS). Required.</summary>
    [Required]
    public string DefaultSenderEmail { get; set; } = string.Empty;

    /// <summary>Default sender display name.</summary>
    public string? DefaultSenderName { get; set; }

    /// <summary>Send timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
