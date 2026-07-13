using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Options;

/// <summary>Azure Communication Services SMS connection options.</summary>
public sealed class AcsSmsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:AzureCommunicationServices:Sms";

    /// <summary>
    /// ACS connection string. When provided, takes precedence over <see cref="Endpoint"/>.
    /// Either <see cref="ConnectionString"/> or <see cref="Endpoint"/> must be set.
    /// Contains the access key — source from Vault, never from plaintext appsettings.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// ACS endpoint URI (e.g. "https://my-acs.communication.azure.com").
    /// Used with <see cref="Azure.Identity.DefaultAzureCredential"/> when
    /// <see cref="ConnectionString"/> is not provided.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Default sender phone number in E.164 format (must start with "+"). Required.</summary>
    [Required]
    public string FromPhoneNumber { get; set; } = string.Empty;

    /// <summary>Send timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
