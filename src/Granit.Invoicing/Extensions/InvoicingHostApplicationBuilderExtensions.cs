using Granit.Analytics.Extensions;
using Granit.Dashboards.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Entities.Extensions;
using Granit.Invoicing.Dashboards;
using Granit.Invoicing.Definitions;
using Granit.Invoicing.Diagnostics;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Entities;
using Granit.Invoicing.Exports;
using Granit.Invoicing.Internal;
using Granit.Invoicing.Metrics;
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
        builder.Services.TryAddTransient<IInvoiceCreditApplier, DefaultInvoiceCreditApplier>();
        builder.Services.AddQueryDefinition<Invoice, InvoiceQueryDefinition>();
        builder.Services.AddExportDefinition<Invoice, InvoiceExportDefinition>();
        builder.Services.AddMetricDefinition<Invoice, int, UnpaidInvoiceCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, decimal, UnpaidInvoiceTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, int, OverdueInvoiceCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, decimal, OverdueInvoiceTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, int, IssuedInvoiceCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, decimal, IssuedInvoiceTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, int, PaidInvoiceCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, decimal, PaidInvoiceTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, decimal, CreditNoteTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<Invoice, int, OverpaidInvoiceCountMetricDefinition>();
        builder.Services.AddDashboardDefinition<InvoicingFinanceOverviewDashboardDefinition>();

        // ADR-040 §Phase 1.F: declares the Invoice UI surface (form / detail /
        // list collection) so the showcase host renders it without per-form
        // scaffolding (story #1565).
        builder.Services.AddEntityDefinition<Invoice, InvoiceEntityDefinition>();

        GranitActivitySourceRegistry.Register(InvoicingActivitySource.Name);
        return builder;
    }
}
