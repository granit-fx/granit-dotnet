using DnsClient;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Diagnostics;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Exports;
using Granit.Hostnames.Options;
using Granit.Hostnames.Queries;
using Granit.Hostnames.Services;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Granit.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        // Default DNS verifier — uses the system resolver with explicit bounds.
        // Replace by registering a custom IHostnameVerifier before calling AddGranitHostnames().
        context.Services.TryAddSingleton<ILookupClient>(sp =>
        {
            HostnamesOptions opts = sp.GetRequiredService<IOptions<HostnamesOptions>>().Value;
            return new LookupClient(new LookupClientOptions
            {
                Timeout = TimeSpan.FromSeconds(opts.DnsQueryTimeoutSeconds),
                Retries = 1,
                UseCache = false,         // always query live — verification must see real DNS state
                ThrowDnsErrors = false,   // DnsResponseException is handled in DnsHostnameVerifier
                ContinueOnDnsError = true,
                UseTcpFallback = true,    // required for TXT records that exceed UDP payload limits
            });
        });
        context.Services.TryAddSingleton<IHostnameVerifier, DnsHostnameVerifier>();

        context.Services.TryAddScoped<IHostnameRegistrationService, HostnameRegistrationService>();
    }
}
