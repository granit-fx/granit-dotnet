using System.Collections.Concurrent;
using System.Reflection;
using Granit.Domain;
using Granit.Notifications.Abstractions;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core SaveChanges interceptor that detects modifications on <see cref="ITrackedEntity"/>
/// and publishes notifications to entity followers via automatic change tracking.
/// </summary>
internal sealed class EntityTrackingInterceptor(
    INotificationPublisher notificationPublisher,
    IClock clock) : SaveChangesInterceptor
{
    // Changes are DETECTED pre-save (entity state is still Modified) but PUBLISHED
    // post-commit: publishing from SavingChangesAsync fired follower notifications for
    // saves that subsequently failed or rolled back. Keyed per context instance —
    // ConditionalWeakTable so a context that never completes cannot leak.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, List<EntityStateChange>> _pendingChanges = [];

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            List<EntityStateChange> changes = DetectChanges(eventData.Context);
            _pendingChanges.Remove(eventData.Context);
            if (changes.Count > 0)
            {
                _pendingChanges.Add(eventData.Context, changes);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pendingChanges.TryGetValue(eventData.Context, out List<EntityStateChange>? changes))
        {
            _pendingChanges.Remove(eventData.Context);

            foreach (EntityStateChange change in changes)
            {
                await notificationPublisher.PublishToEntityFollowersAsync(
                    new EntityStateChangedNotificationType(change.NotificationTypeName, change.Severity),
                    new EntityStateChangedData
                    {
                        EntityType = change.EntityType,
                        EntityId = change.EntityId,
                        PropertyName = change.PropertyName,
                        OldValue = change.OldValue,
                        NewValue = change.NewValue,
                        ChangedAt = clock.Now,
                    },
                    new EntityReference(change.EntityType, change.EntityId),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // The save failed — the rolled-back changes must never notify followers.
        if (eventData.Context is not null)
        {
            _pendingChanges.Remove(eventData.Context);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static List<EntityStateChange> DetectChanges(DbContext context)
    {
        List<EntityStateChange> changes = [];

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified || entry.Entity is not ITrackedEntity trackedEntity)
            {
                continue;
            }

            TrackedEntityMetadata metadata = GetMetadata(entry.Entity.GetType());
            string entityId = trackedEntity.GetEntityId();

            foreach (PropertyEntry property in entry.Properties)
            {
                if (property.IsModified && metadata.TrackedProperties.TryGetValue(property.Metadata.Name, out TrackedPropertyConfig? config))
                {
                    changes.Add(new EntityStateChange
                    {
                        EntityType = metadata.EntityTypeName,
                        EntityId = entityId,
                        PropertyName = property.Metadata.Name,
                        OldValue = property.OriginalValue?.ToString(),
                        NewValue = property.CurrentValue?.ToString(),
                        NotificationTypeName = config.NotificationTypeName,
                        Severity = config.Severity,
                    });
                }
            }
        }

        return changes;
    }

    // Static abstract interface members cannot be read through an instance, so the CLR type
    // is closed over ReadMetadata<TEntity> exactly once per entity type; every subsequent
    // save pays a single ConcurrentDictionary lookup instead of two reflection property reads.
    private static readonly ConcurrentDictionary<Type, TrackedEntityMetadata> MetadataCache = new();

    private static readonly MethodInfo ReadMetadataMethod = typeof(EntityTrackingInterceptor)
        .GetMethod(nameof(ReadMetadata), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static TrackedEntityMetadata GetMetadata(Type entityType) =>
        MetadataCache.GetOrAdd(
            entityType,
            static type => (TrackedEntityMetadata)ReadMetadataMethod.MakeGenericMethod(type).Invoke(null, null)!);

    private static TrackedEntityMetadata ReadMetadata<TEntity>()
        where TEntity : ITrackedEntity =>
        new(TEntity.EntityTypeName, TEntity.TrackedProperties);

    private sealed record TrackedEntityMetadata(
        string EntityTypeName,
        IReadOnlyDictionary<string, TrackedPropertyConfig> TrackedProperties);

    private sealed record EntityStateChange
    {
        public required string EntityType { get; init; }
        public required string EntityId { get; init; }
        public required string PropertyName { get; init; }
        public string? OldValue { get; init; }
        public string? NewValue { get; init; }
        public required string NotificationTypeName { get; init; }
        public NotificationSeverity Severity { get; init; }
    }

    /// <summary>
    /// Internal notification type used for auto-tracked property changes.
    /// </summary>
    private sealed class EntityStateChangedNotificationType(string name, NotificationSeverity severity) : NotificationType<EntityStateChangedData>
    {
        /// <inheritdoc/>
        public override string Name => name;

        /// <inheritdoc/>
        public override NotificationSeverity DefaultSeverity => severity;

        /// <inheritdoc/>
        public override IReadOnlyList<string> DefaultChannels => [NotificationChannels.InApp, NotificationChannels.SignalR];
    }
}
