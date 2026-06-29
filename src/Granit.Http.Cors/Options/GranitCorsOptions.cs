using System.ComponentModel.DataAnnotations;

namespace Granit.Http.Cors.Options;

/// <summary>
/// Configuration options for the Granit CORS module.
/// Bound from the <c>"Http:Cors"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// ISO 27001 compliance: wildcard (<c>*</c>) origins are rejected in non-development
/// environments by the startup validator.
/// </remarks>
public sealed class GranitCorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Http:Cors";

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

    /// <summary>
    /// Returns <see cref="AllowedOrigins"/> with any trailing slash trimmed.
    /// The CORS spec defines an origin as a <c>scheme + host + port</c> tuple
    /// with no path, so an entry like <c>"https://app.x.com/"</c> would silently
    /// fail to match the browser's <c>Origin: https://app.x.com</c> header.
    /// Normalising here preserves the developer copy-paste experience while
    /// keeping <see cref="Internal.ConfigureCorsPolicyOptions"/> and
    /// <see cref="Internal.GranitCorsOptionsValidator"/> aligned.
    /// </summary>
    internal string[] NormalizedOrigins =>
        [.. AllowedOrigins.Select(static o => o?.TrimEnd('/') ?? string.Empty)];
}
