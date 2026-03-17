using Granit.Core.Modularity;
using Granit.Http.Cors.Extensions;
using Granit.Http.Cors.Options;

namespace Granit.Http.Cors;

/// <summary>
/// Granit module for standardized CORS configuration.
/// </summary>
/// <remarks>
/// Registers CORS middleware with a default policy driven by <see cref="GranitCorsOptions"/>.
/// ISO 27001-compliant: wildcard origins are rejected in non-development environments.
/// </remarks>
public sealed class GranitHttpCorsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitCors();
}
