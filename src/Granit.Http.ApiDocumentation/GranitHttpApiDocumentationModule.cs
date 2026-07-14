using Granit.Http.ApiDocumentation.Extensions;
using Granit.Modularity;

namespace Granit.Http.ApiDocumentation;

/// <summary>
/// Granit module for OpenAPI documentation and URL-based API versioning.
/// Reads <c>Http:ApiDocumentation:MajorVersions</c> from configuration to generate
/// one OpenAPI document per declared API version, and registers <c>Asp.Versioning</c>
/// with URL segment (<c>/api/v{version:apiVersion}/resource</c>) and query string readers.
/// </summary>
/// <remarks>
/// Call <c>app.MapGranitOpenApiDocuments()</c> in <c>Program.cs</c> to expose the OpenAPI
/// JSON endpoints, or add the <c>Granit.Http.ApiDocumentation.Scalar</c> package and call
/// <c>app.UseGranitApiDocumentation()</c> to also host the interactive Scalar UI.
/// </remarks>
public sealed class GranitHttpApiDocumentationModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitApiDocumentation(context.ModuleAssemblies);
}
