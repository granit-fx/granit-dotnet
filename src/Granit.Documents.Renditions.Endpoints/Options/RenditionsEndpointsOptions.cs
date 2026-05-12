using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.Renditions.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Documents.Renditions HTTP endpoints.
/// </summary>
/// <remarks>
/// Bound from configuration section <c>Documents:Renditions:Endpoints</c> via
/// <c>AddGranitDocumentsRenditionsEndpoints</c>. Hosts can override the route
/// prefix or the OpenAPI tag without recompiling.
/// </remarks>
public sealed class RenditionsEndpointsOptions
{
    /// <summary>Configuration section key (<c>"Documents:Renditions:Endpoints"</c>).</summary>
    public const string SectionName = "Documents:Renditions:Endpoints";

    /// <summary>
    /// Route prefix for the rendition endpoints, mounted under the host's documents
    /// group. Default: <c>"/documents/{id:guid}/renditions"</c>.
    /// </summary>
    [Required]
    public string RoutePrefix { get; set; } = "/documents/{id:guid}/renditions";

    /// <summary>
    /// OpenAPI tag applied to every rendition endpoint.
    /// Default: <c>"Documents - Renditions"</c>.
    /// </summary>
    [Required]
    public string TagName { get; set; } = "Documents - Renditions";
}
