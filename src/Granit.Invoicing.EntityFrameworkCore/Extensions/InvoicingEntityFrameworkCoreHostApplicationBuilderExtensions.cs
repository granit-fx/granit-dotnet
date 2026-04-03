using Granit.Invoicing.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
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
        builder.Services.AddInternalDbContextEnsurer<InvoicingDbContext>();

        builder.Services.AddScoped<EfInvoiceStore>();
        builder.Services.TryAddScoped<IInvoiceReader>(sp => sp.GetRequiredService<EfInvoiceStore>());
        builder.Services.TryAddScoped<IInvoiceWriter>(sp => sp.GetRequiredService<EfInvoiceStore>());

        return builder;
    }
}
