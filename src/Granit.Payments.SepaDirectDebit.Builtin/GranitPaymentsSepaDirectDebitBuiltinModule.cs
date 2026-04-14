using Granit.Modularity;
using Granit.Payments.SepaDirectDebit.Builtin.Internal;
using Granit.Payments.SepaDirectDebit.Builtin.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Payments.SepaDirectDebit.Builtin;

/// <summary>Self-hosted SEPA Direct Debit: PAIN.008 generation, offline mandate management.</summary>
[DependsOn(typeof(GranitPaymentsSepaDirectDebitModule))]
public sealed class GranitPaymentsSepaDirectDebitBuiltinModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<SepaDirectDebitBuiltinOptions>()
            .BindConfiguration(SepaDirectDebitBuiltinOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddScoped<IDirectDebitProvider, BuiltinDirectDebitProvider>();
        context.Services.TryAddScoped<ICollectionFileGenerator, Pain008Generator>();
        context.Services.AddScoped<IPaymentProvider, SepaDirectDebitPaymentProvider>();
    }
}
