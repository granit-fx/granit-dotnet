using Granit.Invoicing.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.Invoicing entity configurations.</summary>
public static class InvoicingModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Invoicing module.</summary>
    public static ModelBuilder ConfigureInvoicingModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceLineItemConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceExternalReferenceConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceDocumentConfiguration());
        return modelBuilder;
    }
}
