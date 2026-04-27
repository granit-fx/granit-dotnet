using Granit.Invoicing.Domain;
using Granit.Invoicing.EntityFrameworkCore.Internal;
using Granit.Mergeable.Extensions;
using Granit.Parties.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Invoicing.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.Invoicing.</summary>
public static class InvoicingEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit invoicing.</summary>
    public static IHostApplicationBuilder AddGranitInvoicingEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<InvoicingDbContext>(configure);

        builder.Services.AddScoped<EfInvoiceStore>();
        builder.Services.TryAddScoped<IInvoiceReader>(sp => sp.GetRequiredService<EfInvoiceStore>());
        builder.Services.TryAddScoped<IInvoiceWriter>(sp => sp.GetRequiredService<EfInvoiceStore>());

        builder.Services.AddScoped<IQueryableSource<Invoice>, EfInvoiceQueryableSource>();

        // Plugs Invoicing into the Party merge orchestrator. The registration is unconditional —
        // when no IMergeService<Party> is wired up by the host (e.g. an app that doesn't enable
        // merging), the rewriter just sits idle in the DI container at zero runtime cost.
        builder.Services.AddReferenceRewriter<Party, InvoicePartyReferenceRewriter>();

        return builder;
    }
}
