using Granit.Notifications.Abstractions;
using Granit.Notifications.WhatsApp.Options;
using Granit.Settings;
using Granit.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.WhatsApp.Internal;

/// <summary>
/// WhatsApp notification channel that resolves the provider at runtime via Keyed Services.
/// </summary>
/// <remarks>
/// Template language chain: explicit trigger culture → recipient preferred culture (the
/// User level of the settings cascade, resolved by the recipient resolver) → the
/// <c>Granit.Localization.PreferredCulture</c> setting (Tenant → Global levels, soft
/// dependency) → <see cref="WhatsAppChannelOptions.DefaultLanguage"/> as the terminal
/// code-level default.
/// </remarks>
internal sealed class WhatsAppNotificationChannel(
    IServiceProvider serviceProvider,
    IOptions<WhatsAppChannelOptions> options,
    IRecipientResolver recipientResolver,
    ISettingProvider? settingProvider = null) : INotificationChannel
{
    /// <inheritdoc />
    public string Name => NotificationChannels.WhatsApp;

    /// <inheritdoc />
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        IWhatsAppSender sender = serviceProvider.GetRequiredKeyedService<IWhatsAppSender>(options.Value.Provider);

        RecipientInfo? recipient = await recipientResolver.ResolveAsync(context.RecipientUserId, cancellationToken).ConfigureAwait(false);
        if (recipient?.PhoneNumber is null)
        {
            return;
        }

        string language = context.Culture
            ?? recipient.PreferredCulture
            ?? await ResolveCascadeCultureAsync(cancellationToken).ConfigureAwait(false)
            ?? options.Value.DefaultLanguage;

        await sender.SendAsync(new WhatsAppMessage
        {
            To = recipient.PhoneNumber,
            TemplateName = context.NotificationTypeName,
            Language = language,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tenant → Global levels of the settings cascade; the recipient covers the User level.</summary>
    private async Task<string?> ResolveCascadeCultureAsync(CancellationToken cancellationToken)
    {
        if (settingProvider is null)
        {
            return null;
        }

        string? culture = await settingProvider
            .GetOrNullAsync(WellKnownSettingNames.PreferredCulture, cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(culture) ? null : culture;
    }
}
