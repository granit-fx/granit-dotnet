using System.ComponentModel.DataAnnotations;

namespace Granit.Http.Cors.Options;

/// <summary>
/// Configuration options for the Granit CORS module.
/// Bound from the <c>"Cors"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// ISO 27001 compliance: wildcard (<c>*</c>) origins are rejected in non-development
/// environments by the startup validator.
/// </remarks>
public sealed class GranitCorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// Allowed CORS origins. At least one origin must be configured.
    /// Wildcard (<c>*</c>) is forbidden in non-development environments (ISO 27001 compliance).
    /// </summary>
    /// <example>
    /// <code>["https://app.example.com", "https://admin.example.com"]</code>
    /// </example>
    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Whether to include <c>Access-Control-Allow-Credentials: true</c> in CORS responses.
    /// Default: <c>false</c>. Cannot be <c>true</c> when <see cref="AllowedOrigins"/>
    /// contains wildcard (<c>*</c>) — this violates the CORS specification.
    /// </summary>
    public bool AllowCredentials { get; set; }
}
