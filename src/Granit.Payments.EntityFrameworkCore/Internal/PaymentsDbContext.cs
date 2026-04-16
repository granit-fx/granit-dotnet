using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Payments.Domain;
using Granit.Payments.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<PaymentTransaction> Transactions { get; set; } = null!;
    public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents { get; set; } = null!;
    public DbSet<ProviderCustomerMapping> ProviderCustomerMappings { get; set; } = null!;
    public DbSet<PaymentMethodConfiguration> PaymentMethodConfigurations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigurePaymentsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
