using Granit.Modularity;
using Granit.Payments.SepaDirectDebit.Twikey.Internal;
using Granit.Payments.SepaDirectDebit.Twikey.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Payments.SepaDirectDebit.Twikey;

/// <summary>Twikey SEPA DD provider (Phase 3 — stub).</summary>
[DependsOn(typeof(GranitPaymentsSepaDirectDebitModule))]
public sealed class GranitPaymentsSepaDirectDebitTwikeyModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<TwikeyOptions>()
            .BindConfiguration(TwikeyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddScoped<IDirectDebitProvider, TwikeyDirectDebitProvider>();
    }
}
