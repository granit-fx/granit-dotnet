using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiVersioning;
using Granit.Security;

namespace Granit.Http.ApiDocumentation;

/// <summary>
/// Granit module for OpenAPI documentation and Scalar UI.
/// Reads <c>ApiDocumentation:MajorVersions</c> from configuration to generate
/// one OpenAPI document per declared API version.
/// </summary>
/// <remarks>
/// Call <c>app.UseGranitApiDocumentation()</c> in <c>Program.cs</c> to expose
/// the OpenAPI JSON endpoints and the Scalar UI.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiVersioningModule),
    typeof(GranitSecurityModule))]
public sealed class GranitHttpApiDocumentationModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitApiDocumentation();
}
