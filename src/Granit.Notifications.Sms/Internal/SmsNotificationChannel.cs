using Granit.Notifications.Abstractions;
using Granit.Notifications.Rendering;
using Granit.Notifications.Sms.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Sms.Internal;

/// <summary>
/// SMS notification channel that resolves the provider at runtime via Keyed Services.
/// </summary>
internal sealed class SmsNotificationChannel(
    IServiceProvider serviceProvider,
    IOptions<SmsChannelOptions> options,
    IRecipientResolver recipientResolver,
    INotificationContentRenderer? contentRenderer = null) : INotificationChannel
{
    /// <inheritdoc />
    public string Name => NotificationChannels.Sms;

    /// <inheritdoc />
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        ISmsSender sender = serviceProvider.GetRequiredKeyedService<ISmsSender>(options.Value.Provider);

        RecipientInfo? recipient = await recipientResolver.ResolveAsync(context.RecipientUserId, cancellationToken).ConfigureAwait(false);
        if (recipient?.PhoneNumber is null)
        {
            return;
        }

        // Localized template (type-specific or Notifications.Default .txt), minimal fallback
        // when templating is not configured.
        RenderedNotificationContent? rendered = contentRenderer is null
            ? null
            : await contentRenderer.RenderAsync(
                context, recipient, NotificationContentFormat.PlainText, cancellationToken: cancellationToken).ConfigureAwait(false);

        string body = rendered?.Body ?? $"Notification: {context.NotificationTypeName}";

        await sender.SendAsync(new SmsMessage
        {
            To = recipient.PhoneNumber,
            Body = body,
            SenderId = options.Value.SenderId,
        }, cancellationToken).ConfigureAwait(false);
    }
}
