namespace Granit.Http.ApiVersioning.Options;

/// <summary>Configuration options for Granit API versioning.</summary>
public sealed class GranitApiVersioningOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "ApiVersioning";

    /// <summary>
    /// Default API major version assumed when the client does not specify one.
    /// Default: <c>1</c>.
    /// </summary>
    public int DefaultMajorVersion { get; set; } = 1;

    /// <summary>
    /// When <c>true</c>, adds <c>api-supported-versions</c> and <c>api-deprecated-versions</c>
    /// response headers on every response. Useful for audit trails and client deprecation notices.
    /// Default: <c>true</c>.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;
}
