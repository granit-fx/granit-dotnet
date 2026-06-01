using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Hostnames.Diagnostics;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Exports;
using Granit.Hostnames.Queries;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;

namespace Granit.Hostnames;

/// <summary>
/// Granit module for custom hostname management. Registers the managed-hostname query/export pair.
/// The resolver, reader/writer and persistence ship in <c>Granit.Hostnames.EntityFrameworkCore</c>;
/// HTTP endpoints in <c>Granit.Hostnames.Endpoints</c>; DNS verification and provider adapters in
/// their own sibling packages — by design for a layer-pure base package.
/// </summary>
public sealed class GranitHostnamesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GranitActivitySourceRegistry.Register(HostnamesActivitySource.Name);

        context.Services.AddQueryDefinition<ManagedHostname, ManagedHostnameQueryDefinition>();
        context.Services.AddExportDefinition<ManagedHostname, ManagedHostnameExportDefinition>();
    }
}
