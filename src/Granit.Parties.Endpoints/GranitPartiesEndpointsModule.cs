using Granit.Authorization;
using Granit.Modularity;
using Granit.Parties.Endpoints.Internal;
using Granit.Parties.Endpoints.Options;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Parties.Endpoints;

/// <summary>Admin REST API for the Granit.Parties module.</summary>
/// <remarks>
/// Per ADR-020, the QueryDefinition / ExportDefinition registrations live in
/// <c>AddGranitParties</c> on the base module, NOT here — concrete declarative
/// definitions are part of the module's domain contract and stay reachable
/// without taking a runtime dependency on <c>.Endpoints</c>.
/// </remarks>
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

        // Audit writer for the Party merge endpoint. Resolves IAuditingWriter +
        // ICurrentUserService at runtime so hosts that haven't enabled auditing get a
        // resolution failure at the merge call site rather than at module bootstrap.
        context.Services.TryAddScoped<PartyMergeAuditWriter>();
    }
}
