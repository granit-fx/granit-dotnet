using System.Diagnostics;
using System.Text.Json;
using Granit.Encryption;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Messages;

namespace Granit.Notifications.Handlers;

/// <summary>
/// Wolverine handler that fans out a <see cref="NotificationTrigger"/> into one
/// <see cref="DeliverNotificationCommand"/> per recipient x channel.
/// </summary>
public sealed class NotificationFanoutHandler(
    INotificationSubscriptionReader subscriptionReader,
    INotificationPreferenceReader preferenceReader,
    INotificationDefinitionStore definitionStore,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    NotificationsMetrics metrics,
    IStringEncryptionService? encryptionService = null)
{
    /// <summary>
    /// Resolves recipients, loads preferences, filters channels, and produces delivery commands.
    /// </summary>
    public async Task<IEnumerable<DeliverNotificationCommand>> HandleAsync(
        NotificationTrigger trigger,
        CancellationToken cancellationToken)
    {
        using Activity? activity = NotificationsActivitySource.Source.StartActivity(NotificationsActivitySource.Fanout);
        activity?.SetTag("notifications.type", trigger.NotificationTypeName);

        // Decrypt payload when encryption was applied by WolverineNotificationPublisher
        JsonElement data = trigger.Data;
        if (trigger.EncryptedData is not null && encryptionService is not null)
        {
            string? decrypted = encryptionService.Decrypt(trigger.EncryptedData);
            if (decrypted is not null)
            {
                data = JsonDocument.Parse(decrypted).RootElement;
            }
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : trigger.TenantId;
        NotificationDefinition? definition = definitionStore.Get(trigger.NotificationTypeName);
        IReadOnlyList<string> defaultChannels = definition?.DefaultChannels ?? [NotificationChannels.InApp];
        bool allowOptOut = definition?.AllowUserOptOut ?? true;

        // Resolve recipients: explicit list, or subscribers, or entity followers
        IReadOnlyList<string> recipientUserIds = trigger.RecipientUserIds;

        if (recipientUserIds.Count == 0 && trigger.RelatedEntity is not null)
        {
            recipientUserIds = await subscriptionReader.GetEntityFollowerIdsAsync(
                trigger.RelatedEntity.EntityType,
                trigger.RelatedEntity.EntityId,
                tenantId,
                cancellationToken).ConfigureAwait(false);
        }

        if (recipientUserIds.Count == 0)
        {
            recipientUserIds = await subscriptionReader.GetSubscriberIdsAsync(
                trigger.NotificationTypeName,
                tenantId,
                cancellationToken).ConfigureAwait(false);
        }

        if (recipientUserIds.Count == 0)
        {
            activity?.SetTag("notifications.recipient_count", 0);
            activity?.SetTag("notifications.delivery_count", 0);
            return [];
        }

        List<DeliverNotificationCommand> commands = [];

        foreach (string userId in recipientUserIds)
        {
            foreach (string channelName in defaultChannels)
            {
                if (allowOptOut && !await IsChannelEnabledAsync(userId, trigger, channelName, tenantId, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                commands.Add(new DeliverNotificationCommand
                {
                    DeliveryId = guidGenerator.Create(),
                    NotificationId = trigger.NotificationId,
                    NotificationTypeName = trigger.NotificationTypeName,
                    Severity = trigger.Severity,
                    RecipientUserId = userId,
                    ChannelName = channelName,
                    Data = data,
                    RelatedEntity = trigger.RelatedEntity,
                    TenantId = tenantId,
                    OccurredAt = trigger.OccurredAt,
                    Culture = trigger.Culture,
                    RecipientOverride = trigger.RecipientOverride,
                });
            }
        }

        activity?.SetTag("notifications.recipient_count", recipientUserIds.Count);
        activity?.SetTag("notifications.delivery_count", commands.Count);

        metrics.RecordFanoutTriggered(
            currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null,
            trigger.NotificationTypeName);

        return commands;
    }

    private Task<bool> IsChannelEnabledAsync(
        string userId,
        NotificationTrigger trigger,
        string channelName,
        Guid? tenantId,
        CancellationToken cancellationToken) =>
        preferenceReader.IsChannelEnabledAsync(
            userId, trigger.NotificationTypeName, channelName, tenantId, cancellationToken);
}
