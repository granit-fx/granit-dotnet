using Granit.Contacts.Endpoints.Options;
using Granit.Contacts.Exports;
using Granit.Contacts.Queries;
using Granit.DataExchange.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Contacts.Endpoints;

/// <summary>Admin REST API for the Granit.Contacts module.</summary>
[DependsOn(typeof(GranitContactsModule))]
public sealed class GranitContactsEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<ContactEndpointsOptions>()
            .BindConfiguration(ContactEndpointsOptions.SectionName);

        // Mandatory pairing per Granit convention: every admin-visible aggregate must
        // ship both a QueryDefinition and an ExportDefinition, registered together.
        context.Services.AddQueryDefinition<Domain.Contact, ContactQueryDefinition>();
        context.Services.AddExportDefinition<Domain.Contact, ContactExportDefinition>();
    }
}
