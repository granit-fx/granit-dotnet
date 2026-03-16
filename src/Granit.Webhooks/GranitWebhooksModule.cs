using Granit.Core.Modularity;
using Granit.HttpResilience;
using Granit.Timing;
using Granit.Webhooks.Extensions;

namespace Granit.Webhooks;

/// <summary>
/// Granit module for outbound webhook dispatch.
/// </summary>
/// <remarks>
/// Default registrations use in-memory stores and in-process channel dispatch,
/// suitable for development and tests. For production, add
/// <c>Granit.Webhooks.Wolverine</c> for durable outbox dispatch and call
/// <c>AddGranitWebhooksEntityFrameworkCore()</c> for persistent stores.
/// </remarks>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitTimingModule))]
public sealed class GranitWebhooksModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitWebhooks();
}
