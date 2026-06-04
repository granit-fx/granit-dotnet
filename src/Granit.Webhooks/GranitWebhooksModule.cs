using System.Reflection;
using Granit.Encryption;
using Granit.Guids;
using Granit.Http.Resilience;
using Granit.Http.Security;
using Granit.Modularity;
using Granit.Reflection;
using Granit.Timing;
using Granit.Webhooks.Definitions;
using Granit.Webhooks.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Webhooks;

/// <summary>
/// Granit module for outbound webhook dispatch.
/// </summary>
/// <remarks>
/// <para>
/// Default registrations use in-memory stores and in-process channel dispatch,
/// suitable for development and tests. For production, add
/// <c>Granit.Webhooks.Wolverine</c> for durable outbox dispatch and call
/// <c>AddGranitWebhooksEntityFrameworkCore()</c> for persistent stores.
/// </para>
/// <para>
/// Auto-discovers all <see cref="IWebhookEventTypeDefinitionProvider"/> implementations
/// across loaded module assemblies.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpResilienceModule),
    typeof(GranitHttpSecurityModule),
    typeof(GranitTimingModule))]
public sealed class GranitWebhooksModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Builder.AddGranitWebhooks();

        foreach (Assembly assembly in context.ModuleAssemblies)
        {
            IEnumerable<Type> providerTypes = assembly.GetLoadableTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(IWebhookEventTypeDefinitionProvider).IsAssignableFrom(t));

            foreach (Type providerType in providerTypes)
            {
                context.Services.AddSingleton(typeof(IWebhookEventTypeDefinitionProvider), providerType);
            }
        }
    }
}
