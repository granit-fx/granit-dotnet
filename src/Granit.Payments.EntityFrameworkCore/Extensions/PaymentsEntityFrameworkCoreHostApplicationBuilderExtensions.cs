using Granit.Payments.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Payments.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering Payments EF Core persistence.</summary>
public static class PaymentsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddGranitPaymentsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<PaymentsDbContext>(configure);
        builder.Services.AddHostInternalDbContextEnsurer<PaymentsDbContext>();

        builder.Services.AddScoped<EfPaymentTransactionStore>();
        builder.Services.TryAddScoped<IPaymentTransactionReader>(sp => sp.GetRequiredService<EfPaymentTransactionStore>());
        builder.Services.TryAddScoped<IPaymentTransactionWriter>(sp => sp.GetRequiredService<EfPaymentTransactionStore>());

        builder.Services.AddScoped<EfPaymentMethodStore>();
        builder.Services.TryAddScoped<IPaymentMethodReader>(sp => sp.GetRequiredService<EfPaymentMethodStore>());
        builder.Services.TryAddScoped<IPaymentMethodWriter>(sp => sp.GetRequiredService<EfPaymentMethodStore>());

        builder.Services.AddScoped<EfProcessedWebhookEventStore>();
        builder.Services.TryAddScoped<IProcessedWebhookEventStore>(sp => sp.GetRequiredService<EfProcessedWebhookEventStore>());

        builder.Services.AddScoped<EfProviderCustomerMappingStore>();
        builder.Services.TryAddScoped<IProviderCustomerMappingStore>(sp => sp.GetRequiredService<EfProviderCustomerMappingStore>());

        return builder;
    }
}
