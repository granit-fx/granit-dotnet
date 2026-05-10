using Granit.Notifications.Abstractions;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.MobilePush.Internal;

/// <summary>
/// Mobile push notification channel that resolves the provider at runtime via Keyed Services
/// and delivers to all registered device tokens for the recipient.
/// </summary>
internal sealed partial class MobilePushNotificationChannel(
    IServiceProvider serviceProvider,
    IOptions<MobilePushChannelOptions> options,
    IMobilePushTokenReader tokenReader,
    ILogger<MobilePushNotificationChannel> logger) : INotificationChannel
{
    /// <inheritdoc />
    public string Name => NotificationChannels.MobilePush;

    /// <inheritdoc />
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MobilePushToken> tokens = await tokenReader
            .GetTokensAsync(context.RecipientUserId, context.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tokens.Count == 0)
        {
            LogNoTokens(context.RecipientUserId);
            return;
        }

        IMobilePushSender sender = serviceProvider
            .GetRequiredKeyedService<IMobilePushSender>(options.Value.Provider);

        await sender.SendAsync(new MobilePushMessage
        {
            DeviceTokens = tokens.Select(t => t.DeviceToken).ToList(),
            Title = context.NotificationTypeName,
            Body = $"Notification: {context.NotificationTypeName}",
            Data = context.Data,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No mobile push tokens for user {UserId}, skipping")]
    private partial void LogNoTokens(string userId);
}
