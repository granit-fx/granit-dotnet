using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.GoogleFcm.Options;

/// <summary>Configuration for Firebase Cloud Messaging.</summary>
public sealed class GoogleFcmOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:GoogleFcm";

    /// <summary>Firebase project ID.</summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Service account JSON key (resolved from Vault at runtime).
    /// Used to obtain OAuth 2.0 access tokens for FCM HTTP v1 API.
    /// </summary>
    [Required]
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>FCM API base address.</summary>
    [Required, Url]
    public string BaseAddress { get; set; } = "https://fcm.googleapis.com/";

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
