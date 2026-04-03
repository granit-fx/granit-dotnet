using Granit.Diagnostics;
using Granit.Invoicing.Definitions;
using Granit.Invoicing.Diagnostics;
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
        GranitActivitySourceRegistry.Register(InvoicingActivitySource.Name);
        return builder;
    }
}
