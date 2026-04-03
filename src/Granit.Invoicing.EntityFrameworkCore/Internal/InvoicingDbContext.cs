using Granit.DataFiltering;
using Granit.Invoicing.Domain;
using Granit.Invoicing.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

internal sealed class InvoicingDbContext(
    DbContextOptions<InvoicingDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Invoice> Invoices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureInvoicingModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
