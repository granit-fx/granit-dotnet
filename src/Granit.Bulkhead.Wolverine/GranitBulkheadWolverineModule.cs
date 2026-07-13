using Granit.Modularity;

namespace Granit.Bulkhead.Wolverine;

/// <summary>
/// Granit module for the Wolverine binding of bulkhead isolation. Depends on the
/// framework-pure <see cref="GranitBulkheadModule"/> (registry, quota providers,
/// <see cref="TenantPartitionedBulkhead"/>).
/// </summary>
/// <remarks>
/// The <see cref="BulkheadMiddleware"/> is discovered by Wolverine by convention; register it in
/// your Wolverine setup so it runs only for message types decorated with
/// <see cref="Attributes.BulkheadAttribute"/>:
/// <code>
/// opts.Policies.AddMiddleware&lt;BulkheadMiddleware&gt;(
///     chain => chain.MessageType.GetCustomAttributes(typeof(BulkheadAttribute), true).Length > 0);
/// </code>
/// </remarks>
[DependsOn(typeof(GranitBulkheadModule))]
public sealed class GranitBulkheadWolverineModule : GranitModule;
