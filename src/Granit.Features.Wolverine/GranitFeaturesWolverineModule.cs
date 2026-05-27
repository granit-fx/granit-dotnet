using Granit.Modularity;

namespace Granit.Features.Wolverine;

/// <summary>
/// Granit module for the Wolverine binding of feature management. Depends on the framework-pure
/// <see cref="GranitFeaturesModule"/> (resolution, cache, <see cref="IFeatureChecker"/>).
/// </summary>
/// <remarks>
/// The <see cref="RequiresFeatureMiddleware"/> is discovered by Wolverine by convention; register
/// it in your Wolverine setup so it runs only for message types decorated with
/// <see cref="Attributes.RequiresFeatureAttribute"/>:
/// <code>
/// opts.Policies.AddMiddleware&lt;RequiresFeatureMiddleware&gt;(
///     chain => chain.MessageType.HasAttribute&lt;RequiresFeatureAttribute&gt;());
/// </code>
/// </remarks>
[DependsOn(typeof(GranitFeaturesModule))]
public sealed class GranitFeaturesWolverineModule : GranitModule;
