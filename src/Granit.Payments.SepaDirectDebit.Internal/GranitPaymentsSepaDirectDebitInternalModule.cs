using Granit.Modularity;
using Granit.Payments.SepaDirectDebit.Internal.Internal;
using Granit.Payments.SepaDirectDebit.Internal.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Payments.SepaDirectDebit.Internal;

/// <summary>Self-hosted SEPA Direct Debit: PAIN.008 generation, offline mandate management.</summary>
[DependsOn(typeof(GranitPaymentsSepaDirectDebitModule))]
public sealed class GranitPaymentsSepaDirectDebitInternalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<SepaDirectDebitInternalOptions>()
            .BindConfiguration(SepaDirectDebitInternalOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddScoped<IDirectDebitProvider, InternalDirectDebitProvider>();
        context.Services.TryAddScoped<ICollectionFileGenerator, Pain008Generator>();
        context.Services.AddScoped<IPaymentProvider, SepaDirectDebitPaymentProvider>();
    }
}
