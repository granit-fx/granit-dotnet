using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.Scaleway.Options;

/// <summary>Scaleway Transactional Email connection options.</summary>
public sealed class ScalewayEmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:Scaleway";

    /// <summary>Scaleway API secret key. Required.</summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Scaleway project ID. Required.</summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Default sender email address (must be verified in Scaleway).</summary>
    [Required]
    public string DefaultSenderEmail { get; set; } = string.Empty;

    /// <summary>Default sender display name.</summary>
    public string DefaultSenderName { get; set; } = string.Empty;

    /// <summary>Scaleway region. Default: <c>fr-par</c> (Paris).</summary>
    public string Region { get; set; } = "fr-par";

    /// <summary>Scaleway Transactional Email API base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.scaleway.com/transactional-email/v1alpha1";

    /// <summary>Send timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
