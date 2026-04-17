using Granit.Modularity;
using Granit.Payments.HealthChecks;
using Granit.Payments.SepaTransfer.Internal;
using Granit.Payments.SepaTransfer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Payments.SepaTransfer;

/// <summary>
/// Self-hosted SEPA bank transfer payment provider. Zero external dependency.
/// </summary>
[DependsOn(typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsSepaTransferModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<SepaTransferOptions>()
            .BindConfiguration(SepaTransferOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddScoped<StructuredReferenceGenerator>();
        context.Services.AddScoped<IPaymentProvider, SepaTransferPaymentProvider>();
        context.Services.AddScoped<ICheckoutSessionFactory, SepaTransferCheckoutSessionFactory>();
        context.Services.TryAddScoped<IBankReconciliationProcessor, DefaultBankReconciliationProcessor>();

        // Register CAMT.053 parser
        context.Services.AddSingleton<IBankStatementParser, Camt053Parser>();

        context.Services.AddHealthChecks().AddGranitPaymentProviderHealthCheck("sepa-transfer");
    }
}
