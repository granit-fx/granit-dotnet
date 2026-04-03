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
        builder.Services.AddInternalDbContextEnsurer<PaymentsDbContext>();

        builder.Services.AddScoped<EfPaymentTransactionStore>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPaymentTransactionReader>(sp => sp.GetRequiredService<EfPaymentTransactionStore>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<IPaymentTransactionWriter>(sp => sp.GetRequiredService<EfPaymentTransactionStore>()));

        builder.Services.AddScoped<EfPaymentMethodStore>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPaymentMethodReader>(sp => sp.GetRequiredService<EfPaymentMethodStore>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<IPaymentMethodWriter>(sp => sp.GetRequiredService<EfPaymentMethodStore>()));

        builder.Services.AddScoped<EfProcessedWebhookEventStore>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IProcessedWebhookEventStore>(sp => sp.GetRequiredService<EfProcessedWebhookEventStore>()));

        return builder;
    }
}
