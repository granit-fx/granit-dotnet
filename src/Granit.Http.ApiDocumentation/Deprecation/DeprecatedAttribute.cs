namespace Granit.Http.ApiDocumentation.Deprecation;

/// <summary>
/// Marks an endpoint as deprecated. The deprecation headers middleware
/// (auto-registered by <c>AddGranitApiDocumentation</c>) emits <c>Deprecation</c>,
/// <c>Sunset</c>, and <c>Link</c> response headers per RFC 8594, and
/// <c>DeprecationOperationTransformer</c> flags the operation as
/// <c>deprecated: true</c> in the OpenAPI document.
/// </summary>
/// <remarks>
/// <para>Apply to Minimal API endpoints via <c>.WithMetadata(new DeprecatedAttribute(...))</c>
/// (or the <c>.Deprecated(...)</c> extension) or to MVC actions as a standard attribute.
/// No extra wiring is required — metadata alone is sufficient.</para>
/// <para>
/// When applied, every response includes:
/// <list type="bullet">
///   <item><c>Deprecation: true</c></item>
///   <item><c>Sunset: &lt;HTTP-date&gt;</c> (when <see cref="SunsetDate"/> is set)</item>
///   <item><c>Link: &lt;url&gt;; rel="sunset"</c> (when <see cref="Link"/> is set)</item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DeprecatedAttribute : Attribute
{
    /// <summary>
    /// Date after which the endpoint will be removed. Converted to an HTTP-date
    /// in the <c>Sunset</c> header (RFC 7231 §7.1.1.1, midnight UTC).
    /// When <c>null</c>, only the <c>Deprecation: true</c> header is emitted.
    /// </summary>
    public DateOnly? SunsetDate { get; init; }

    /// <summary>
    /// URL to migration documentation. Emitted as a <c>Link</c> header with
    /// <c>rel="sunset"</c> (RFC 8594 §5).
    /// </summary>
    public string? Link { get; init; }
}
