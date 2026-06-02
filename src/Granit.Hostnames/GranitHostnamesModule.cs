using DnsClient;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Diagnostics;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Exports;
using Granit.Hostnames.Queries;
using Granit.Hostnames.Services;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Granit.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Hostnames;

/// <summary>
/// Granit module for custom hostname management. Registers the managed-hostname query/export pair.
/// The resolver, reader/writer and persistence ship in <c>Granit.Hostnames.EntityFrameworkCore</c>;
/// HTTP endpoints in <c>Granit.Hostnames.Endpoints</c>; DNS verification and provider adapters in
/// their own sibling packages — by design for a layer-pure base package.
/// </summary>
[DependsOn(typeof(GranitWorkflowModule))]
public sealed class GranitHostnamesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GranitActivitySourceRegistry.Register(HostnamesActivitySource.Name);

        context.Services.TryAddSingleton<HostnamesMetrics>();

        context.Services.AddQueryDefinition<ManagedHostname, ManagedHostnameQueryDefinition>();
        context.Services.AddExportDefinition<ManagedHostname, ManagedHostnameExportDefinition>();

        // Default DNS verifier — uses the system resolver. Replace by registering a custom
        // IHostnameVerifier before calling AddGranitHostnames().
        context.Services.TryAddSingleton<ILookupClient>(_ => new LookupClient());
        context.Services.TryAddSingleton<IHostnameVerifier, DnsHostnameVerifier>();

        context.Services.AddScoped<IHostnameRegistrationService, HostnameRegistrationService>();
    }
}
