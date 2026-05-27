using Granit.Features;
using Granit.Modularity;

namespace Granit.Http.Features;

/// <summary>
/// Granit module for the ASP.NET Core binding of feature management. Depends on the
/// framework-pure <see cref="GranitFeaturesModule"/> (resolution, cache,
/// <see cref="IFeatureChecker"/>).
/// </summary>
/// <remarks>
/// Guard Minimal-API endpoints with <c>.RequiresFeature("name")</c>
/// (see <see cref="AspNetCore.FeatureEndpointConventionBuilderExtensions"/>); a disabled feature
/// throws <see cref="Granit.Features.Exceptions.FeatureNotEnabledException"/>, mapped to HTTP 403.
/// </remarks>
[DependsOn(typeof(GranitFeaturesModule))]
public sealed class GranitHttpFeaturesModule : GranitModule;
