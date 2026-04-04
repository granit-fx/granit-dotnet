using GoCardless;
using Granit.Modularity;
using Granit.Payments.SepaDirectDebit.GoCardless.Internal;
using Granit.Payments.SepaDirectDebit.GoCardless.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Payments.SepaDirectDebit.GoCardless;

/// <summary>GoCardless SEPA Direct Debit provider.</summary>
[DependsOn(typeof(GranitPaymentsSepaDirectDebitModule))]
public sealed class GranitPaymentsSepaDirectDebitGoCardlessModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<GoCardlessOptions>()
            .BindConfiguration(GoCardlessOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddScoped(sp =>
        {
            IOptions<GoCardlessOptions> opts = sp.GetRequiredService<IOptions<GoCardlessOptions>>();
            GoCardlessClient.Environment env = opts.Value.UseSandbox
                ? GoCardlessClient.Environment.SANDBOX
                : GoCardlessClient.Environment.LIVE;
            return GoCardlessClient.Create(opts.Value.AccessToken, env);
        });

        context.Services.TryAddScoped<IDirectDebitProvider, GoCardlessDirectDebitProvider>();
        context.Services.AddSingleton<IPaymentWebhookVerifier, GoCardlessWebhookVerifier>();
    }
}
