using Granit.Authorization;
using Granit.DataExchange.Extensions;
using Granit.Modularity;
using Granit.Parties.Endpoints.Internal;
using Granit.Parties.Endpoints.Options;
using Granit.Parties.Exports;
using Granit.Parties.Queries;
using Granit.QueryEngine.Extensions;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Parties.Endpoints;

/// <summary>Admin REST API for the Granit.Parties module.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitPartiesModule),
    typeof(GranitValidationModule))]
public sealed class GranitPartiesEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<PartyEndpointsOptions>()
            .BindConfiguration(PartyEndpointsOptions.SectionName);

        // Mandatory pairing per Granit convention: every admin-visible aggregate must
        // ship both a QueryDefinition and an ExportDefinition, registered together.
        context.Services.AddQueryDefinition<Domain.Party, PartyQueryDefinition>();
        context.Services.AddExportDefinition<Domain.Party, PartyExportDefinition>();

        // Audit writer for the Party merge endpoint. Resolves IAuditingWriter +
        // ICurrentUserService at runtime so hosts that haven't enabled auditing get a
        // resolution failure at the merge call site rather than at module bootstrap.
        context.Services.TryAddScoped<PartyMergeAuditWriter>();
    }
}
