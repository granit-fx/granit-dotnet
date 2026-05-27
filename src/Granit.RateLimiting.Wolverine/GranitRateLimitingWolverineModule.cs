using Granit.Modularity;

namespace Granit.RateLimiting.Wolverine;

/// <summary>
/// Granit module for the Wolverine binding of rate limiting. Depends on the framework-pure
/// <see cref="GranitRateLimitingModule"/> (counter store, quota provider,
/// <see cref="TenantPartitionedRateLimiter"/>).
/// </summary>
/// <remarks>
/// The <see cref="RateLimitMiddleware"/> is discovered by Wolverine by convention; register it in
/// your Wolverine setup so it runs only for message types decorated with
/// <see cref="Attributes.RateLimitedAttribute"/>:
/// <code>
/// opts.Policies.AddMiddleware&lt;RateLimitMiddleware&gt;(
///     chain => chain.MessageType.GetCustomAttributes(typeof(RateLimitedAttribute), true).Length > 0);
/// </code>
/// </remarks>
[DependsOn(typeof(GranitRateLimitingModule))]
public sealed class GranitRateLimitingWolverineModule : GranitModule;
