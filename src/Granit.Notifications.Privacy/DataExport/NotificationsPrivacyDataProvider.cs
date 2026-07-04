using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using Granit.QueryEngine;

namespace Granit.Notifications.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.Notifications. Exports the user's in-app inbox,
/// notification preferences, and topic/entity subscriptions as a single staged JSON
/// fragment.
/// </summary>
/// <remarks>
/// The inbox is paged via <see cref="IUserNotificationReader.GetListAsync"/> up to
/// <see cref="NotificationsExportLimit"/>. Exceeding it flags the payload as
/// <c>truncated</c>. Preferences and subscriptions are fetched in full — these collections
/// are bounded by registered notification types/topics and stay small.
/// </remarks>
public sealed class NotificationsPrivacyDataProvider(
    IUserNotificationReader notificationReader,
    INotificationPreferenceReader preferenceReader,
    INotificationSubscriptionReader subscriptionReader,
    ICurrentTenant currentTenant,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <summary>Maximum inbox items exported for a single user.</summary>
    public const int NotificationsExportLimit = 5_000;

    /// <inheritdoc />
    public static string ProviderName => "notifications";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.Notifications";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string recipientUserId = context.SubjectUserId.ToString();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        PagedResult<UserNotification> firstPage = await notificationReader
            .GetListAsync(recipientUserId, tenantId, page: 1, pageSize: 1, cancellationToken)
            .ConfigureAwait(false);
        if (firstPage.Items.Count > 0)
        {
            return true;
        }

        IReadOnlyList<NotificationPreference> preferences = await preferenceReader
            .GetListAsync(recipientUserId, tenantId, cancellationToken).ConfigureAwait(false);
        if (preferences.Count > 0)
        {
            return true;
        }

        IReadOnlyList<NotificationSubscription> subscriptions = await subscriptionReader
            .GetUserSubscriptionsAsync(recipientUserId, tenantId, cancellationToken).ConfigureAwait(false);
        return subscriptions.Count > 0;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExportFragment> ExportAsync(
        PrivacyExportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExportCoreAsync(context, cancellationToken);
    }

    private async IAsyncEnumerable<ExportFragment> ExportCoreAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string recipientUserId = context.SubjectUserId.ToString();
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
            yield break;
        }

        var dto = new NotificationsExportDto(
            UserId: context.SubjectUserId,
            ExportedInboxItems: inbox.Count,
            InboxTruncated: truncated,
            InboxLimit: NotificationsExportLimit,
            Inbox: inbox.ConvertAll(Map),
            Preferences: preferences.Select(p => new NotificationsPreferenceDto(
                p.NotificationTypeName, p.ChannelName, p.IsEnabled)).ToList(),
            Subscriptions: subscriptions.Select(s => new NotificationsSubscriptionDto(
                s.Id, s.NotificationTypeName, s.EntityType, s.EntityId, s.CreatedAt)).ToList());

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "notifications.json", dto, cancellationToken)
            .ConfigureAwait(false);
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
