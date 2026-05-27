using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Granit.Http.Features.AspNetCore;

/// <summary>
/// Extension methods for applying feature checks to Minimal API endpoint groups and routes.
/// </summary>
public static class FeatureEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="RequiresFeatureEndpointFilter"/> that returns HTTP 403 when
    /// <paramref name="featureName"/> is not enabled for the current tenant/plan context.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapPost("/consultations/start", ...)
    ///    .RequiresFeature(AcmeFeatures.VideoConsultation.Name);
    ///
    /// // Or on a group:
    /// RouteGroupBuilder export = app.MapGroup("/export")
    ///    .RequiresFeature(AcmeFeatures.ExportPdf.Name);
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder RequiresFeature(
        this IEndpointConventionBuilder builder,
        string featureName) =>
        builder.AddEndpointFilter(new RequiresFeatureEndpointFilter(featureName));
}
