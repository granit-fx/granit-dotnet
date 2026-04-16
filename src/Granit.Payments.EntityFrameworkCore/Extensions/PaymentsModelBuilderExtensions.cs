using Granit.Payments.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.Payments entity configurations.</summary>
public static class PaymentsModelBuilderExtensions
{
    public static ModelBuilder ConfigurePaymentsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PaymentTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentMethodEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new RefundConfiguration());
        modelBuilder.ApplyConfiguration(new DisputeConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedWebhookEventConfiguration());
        modelBuilder.ApplyConfiguration(new ProviderCustomerMappingConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentMethodConfigurationEntityTypeConfiguration());
        return modelBuilder;
    }
}
