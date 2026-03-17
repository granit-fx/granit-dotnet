namespace Granit.Http.ApiVersioning.Deprecation;

/// <summary>
/// Marks an endpoint as deprecated and triggers <c>Deprecation</c> and <c>Sunset</c>
/// response headers per RFC 8594.
/// </summary>
/// <remarks>
/// <para>Apply to Minimal API endpoints via <c>.WithMetadata(new DeprecatedAttribute(...))</c>
/// or to MVC actions as a standard attribute.</para>
/// <para>
/// When applied, every response includes:
/// <list type="bullet">
///   <item><c>Deprecation: true</c></item>
///   <item><c>Sunset: &lt;HTTP-date&gt;</c> (when <see cref="SunsetDate"/> is set)</item>
///   <item><c>Link: &lt;url&gt;; rel="deprecation"</c> (when <see cref="Link"/> is set)</item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DeprecatedAttribute : Attribute
{
    /// <summary>
    /// Date after which the endpoint will be removed, in ISO 8601 format (e.g. <c>"2025-11-01"</c>).
    /// Converted to an HTTP-date in the <c>Sunset</c> header (RFC 7231 §7.1.1.1).
    /// When <c>null</c>, only the <c>Deprecation: true</c> header is emitted.
    /// </summary>
    public string? SunsetDate { get; init; }

    /// <summary>
    /// URL to migration documentation. Emitted as a <c>Link</c> header with <c>rel="deprecation"</c>.
    /// </summary>
    public string? Link { get; init; }
}
