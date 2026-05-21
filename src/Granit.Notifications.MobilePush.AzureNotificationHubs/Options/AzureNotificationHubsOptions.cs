using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.MobilePush.AzureNotificationHubs.Options;

/// <summary>Azure Notification Hubs connection options.</summary>
public sealed class AzureNotificationHubsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:MobilePush:AzureNotificationHubs";

    /// <summary>
    /// Azure Notification Hubs connection string. Required.
    /// Must be stored in a secret manager (Vault, Azure Key Vault) — never in appsettings.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Azure Notification Hub name. Required.</summary>
    [Required]
    public string HubName { get; set; } = string.Empty;

    /// <summary>Send timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
