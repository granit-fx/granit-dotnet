using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Federated.GoogleCloud.Options;

/// <summary>Configuration for Google Cloud Identity Platform (Firebase Auth) admin operations.</summary>
public sealed class GoogleCloudIdentityOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Federated:GoogleCloud";

    /// <summary>GCP project ID. Required.</summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to a service account key JSON file.
    /// When <c>null</c>, Application Default Credentials (ADC) are used.
    /// </summary>
    public string? CredentialFilePath { get; set; }

    /// <summary>
    /// Custom claims key used as roles (e.g. <c>"roles"</c>).
    /// Firebase Auth does not have native roles; custom claims are used instead.
    /// Default: <c>"roles"</c>.
    /// </summary>
    public string RolesClaimKey { get; set; } = "roles";

    /// <summary>API call timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
