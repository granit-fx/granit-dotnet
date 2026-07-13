using System.Text.Json;
using Granit.Domain;
using Granit.Encryption;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Messages;
using Granit.Timing;
using Wolverine;

namespace Granit.Notifications.Wolverine.Internal;

/// <summary>
/// <see cref="INotificationPublisher"/> implementation that publishes
/// <see cref="NotificationTrigger"/> messages into the Wolverine Outbox
/// for durable, transactional dispatch.
/// </summary>
internal sealed class WolverineNotificationPublisher(
    IMessageBus messageBus,
    ICurrentTenant currentTenant,
    IClock clock,
    IGuidGenerator guidGenerator,
    IStringEncryptionService? encryptionService = null) : INotificationPublisher
{
    public ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        CancellationToken cancellationToken = default) where TData : notnull =>
        PublishAsync(notificationType, data, recipientUserIds, relatedEntity: null, cancellationToken);

    public async ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        EntityReference? relatedEntity,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity);
        trigger = trigger with { RecipientUserIds = recipientUserIds };
        await messageBus.PublishAsync(trigger).ConfigureAwait(false);
    }

    public async ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        RecipientInfo recipientOverride,
        EntityReference? relatedEntity = null,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        ArgumentNullException.ThrowIfNull(recipientOverride);
        if (recipientUserIds.Count > 1)
        {
            throw new ArgumentException(
                "RecipientOverride supports only a single recipient.", nameof(recipientUserIds));
        }

        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity);
        trigger = trigger with { RecipientUserIds = recipientUserIds, RecipientOverride = recipientOverride };
        await messageBus.PublishAsync(trigger).ConfigureAwait(false);
    }

    public async ValueTask PublishToSubscribersAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity: null);
        await messageBus.PublishAsync(trigger).ConfigureAwait(false);
    }

    public async ValueTask PublishToEntityFollowersAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        EntityReference relatedEntity,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity);
        await messageBus.PublishAsync(trigger).ConfigureAwait(false);
    }

    private NotificationTrigger BuildTrigger<TData>(
        NotificationType<TData> notificationType,
        TData data,
        EntityReference? relatedEntity) where TData : notnull
    {
        JsonElement jsonData = JsonSerializer.SerializeToElement(data);

        if (encryptionService is not null)
        {
            string plainJson = JsonSerializer.Serialize(data);
            string encrypted = encryptionService.Encrypt(plainJson);
            return new()
            {
                NotificationId = guidGenerator.Create(),
                NotificationTypeName = notificationType.Name,
                Severity = notificationType.DefaultSeverity,
                Data = default,
                EncryptedData = encrypted,
                RelatedEntity = relatedEntity,
                TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
                OccurredAt = clock.Now,
            };
        }

        return new()
        {
            NotificationId = guidGenerator.Create(),
            NotificationTypeName = notificationType.Name,
            Severity = notificationType.DefaultSeverity,
            Data = jsonData,
            RelatedEntity = relatedEntity,
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            OccurredAt = clock.Now,
        };
    }
}
