using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Invoicing.Definitions;
using Granit.Invoicing.Diagnostics;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Exports;
using Granit.Invoicing.Internal;
using Granit.Invoicing.Queries;
using Granit.QueryEngine.Extensions;
using Granit.Workflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Invoicing.Extensions;

/// <summary>Extension methods for registering the Granit invoicing infrastructure.</summary>
public static class InvoicingHostApplicationBuilderExtensions
{
    /// <summary>Adds the Granit invoicing infrastructure.</summary>
    public static IHostApplicationBuilder AddGranitInvoicing(this IHostApplicationBuilder builder)
    {
        builder.Services.AddWorkflow(InvoiceWorkflows.Default);
        builder.Services.TryAddSingleton<InvoicingMetrics>();
        builder.Services.TryAddTransient<IInvoiceCreationService, DefaultInvoiceCreationService>();
        builder.Services.AddQueryDefinition<Invoice, InvoiceQueryDefinition>();
        builder.Services.AddExportDefinition<Invoice, InvoiceExportDefinition>();
        GranitActivitySourceRegistry.Register(InvoicingActivitySource.Name);
        return builder;
    }
}
