using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Privacy.DataExport;
using Granit.QueryEngine;

namespace Granit.Notifications.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.Notifications. Exports the user's in-app inbox,
/// notification preferences, and topic/entity subscriptions as a single JSON fragment.
/// </summary>
/// <remarks>
/// The inbox is paged via <see cref="IUserNotificationReader.GetListAsync"/> up to
/// <see cref="NotificationsExportLimit"/>. Exceeding it flags the payload as
/// <c>truncated</c>. Preferences and subscriptions are fetched in full — these
/// collections are bounded by registered notification types/topics and stay small.
/// </remarks>
public sealed class NotificationsPrivacyDataProvider(
    IUserNotificationReader notificationReader,
    INotificationPreferenceReader preferenceReader,
    INotificationSubscriptionReader subscriptionReader,
    ICurrentTenant currentTenant) : IPrivacyDataProvider
{
    /// <summary>Maximum inbox items exported for a single user.</summary>
    public const int NotificationsExportLimit = 5_000;

    /// <inheritdoc />
    public static string ProviderName => "notifications";

    /// <inheritdoc />
    public static string ContentType => "application/json";

    /// <inheritdoc />
    public static string FileName(Guid requestId) => "notifications.json";

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        string recipientUserId = userId.ToString();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        List<UserNotification> inbox = [];
        int page = 1;
        const int PageSize = QueryEngineDefaults.DefaultPageSize;

        while (inbox.Count < NotificationsExportLimit)
        {
            PagedResult<UserNotification> pageResult = await notificationReader
                .GetListAsync(recipientUserId, tenantId, page, PageSize, cancellationToken)
                .ConfigureAwait(false);

            if (pageResult.Items.Count == 0)
            {
                break;
            }

            inbox.AddRange(pageResult.Items);
            if (pageResult.Items.Count < PageSize)
            {
                break;
            }
            page++;
        }

        bool truncated = inbox.Count > NotificationsExportLimit;
        if (truncated)
        {
            inbox = inbox.Take(NotificationsExportLimit).ToList();
        }

        IReadOnlyList<NotificationPreference> preferences = await preferenceReader
            .GetListAsync(recipientUserId, tenantId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<NotificationSubscription> subscriptions = await subscriptionReader
            .GetUserSubscriptionsAsync(recipientUserId, tenantId, cancellationToken).ConfigureAwait(false);

        if (inbox.Count == 0 && preferences.Count == 0 && subscriptions.Count == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        NotificationsExportDto dto = new(
            UserId: userId,
            ExportedInboxItems: inbox.Count,
            InboxTruncated: truncated,
            InboxLimit: NotificationsExportLimit,
            Inbox: inbox.Select(Map).ToList(),
            Preferences: preferences.Select(p => new NotificationsPreferenceDto(
                p.NotificationTypeName, p.ChannelName, p.IsEnabled)).ToList(),
            Subscriptions: subscriptions.Select(s => new NotificationsSubscriptionDto(
                s.Id, s.NotificationTypeName, s.EntityType, s.EntityId, s.CreatedAt)).ToList());

        return JsonSerializer.SerializeToUtf8Bytes(dto, ExportJsonOptions);
    }

    private static NotificationsInboxDto Map(UserNotification notification) =>
        new(
            notification.Id,
            notification.NotificationTypeName,
            notification.Severity.ToString(),
            notification.State.ToString(),
            notification.CreatedAt,
            notification.ReadAt,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.Data);

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

internal sealed record NotificationsExportDto(
    Guid UserId,
    int ExportedInboxItems,
    bool InboxTruncated,
    int InboxLimit,
    IReadOnlyList<NotificationsInboxDto> Inbox,
    IReadOnlyList<NotificationsPreferenceDto> Preferences,
    IReadOnlyList<NotificationsSubscriptionDto> Subscriptions);

internal sealed record NotificationsInboxDto(
    Guid Id,
    string NotificationTypeName,
    string Severity,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    string? RelatedEntityType,
    string? RelatedEntityId,
    JsonElement Data);

internal sealed record NotificationsPreferenceDto(
    string NotificationTypeName,
    string ChannelName,
    bool IsEnabled);

internal sealed record NotificationsSubscriptionDto(
    Guid Id,
    string? NotificationTypeName,
    string? EntityType,
    string? EntityId,
    DateTimeOffset CreatedAt);
