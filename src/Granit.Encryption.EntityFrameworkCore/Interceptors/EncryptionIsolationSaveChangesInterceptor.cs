using System.Collections.Concurrent;
using System.Reflection;
using Granit.Encryption.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Encryption.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that encrypts properties marked with
/// <c>[Encrypted(KeyIsolation = true)]</c> before <c>SaveChanges</c>,
/// using a per-entity key from <see cref="IEntityEncryptionKeyStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Must be registered AFTER <c>AuditedEntityInterceptor</c> to ensure entity IDs
/// are assigned before encryption runs. Use <c>UseGranitEncryptionInterceptors(sp)</c>
/// after <c>UseGranitInterceptors(sp)</c>.
/// </para>
/// <para>
/// Properties without <c>KeyIsolation = true</c> are handled by
/// <see cref="EncryptedStringConverter"/> (shared key ring) — this interceptor
/// skips them.
/// </para>
/// </remarks>
public sealed partial class EncryptionIsolationSaveChangesInterceptor(
    IEntityEncryptionKeyStore keyStore,
    ILogger<EncryptionIsolationSaveChangesInterceptor> logger) : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> IsolatedPropertiesCache = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EncryptIsolatedPropertiesAsync(eventData.Context, CancellationToken.None)
            .GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await EncryptIsolatedPropertiesAsync(eventData.Context, cancellationToken)
            .ConfigureAwait(false);
        return await base.SavingChangesAsync(eventData, result, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EncryptIsolatedPropertiesAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                await EncryptEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task EncryptEntryAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        PropertyInfo[] isolatedProperties = GetIsolatedProperties(entry.Entity.GetType());
        if (isolatedProperties.Length == 0)
        {
            return;
        }

        string entityType = entry.Entity.GetType().Name;
        string entityId = GetEntityId(entry);

        if (string.IsNullOrEmpty(entityId))
        {
            LogSkippedNoId(logger, entityType);
            return;
        }

        byte[] key = await keyStore.GetOrCreateKeyAsync(entityType, entityId, cancellationToken)
            .ConfigureAwait(false);

        EncryptProperties(entry, isolatedProperties, key);
        LogEncryptedProperties(logger, entityType, entityId, isolatedProperties.Length);
    }

    private static void EncryptProperties(EntityEntry entry, PropertyInfo[] properties, byte[] key)
    {
        foreach (PropertyInfo property in properties)
        {
            string? plainText = property.GetValue(entry.Entity) as string;
            if (plainText is null)
            {
                continue;
            }

            string cipherText = IsolatedFieldEncryptor.Encrypt(key, plainText);
            property.SetValue(entry.Entity, cipherText);

            if (entry.State is EntityState.Modified)
            {
                entry.Property(property.Name).IsModified = true;
            }
        }
    }

    private static PropertyInfo[] GetIsolatedProperties(Type entityType) =>
        IsolatedPropertiesCache.GetOrAdd(entityType, static type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static p =>
                    p.PropertyType == typeof(string) &&
                    p.GetCustomAttribute<EncryptedAttribute>() is { KeyIsolation: true })
                .ToArray());

    private static string GetEntityId(EntityEntry entry)
    {
        // Use the primary key value — handles both Guid and composite keys
        object?[] keyValues = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue)
            .ToArray();

        if (keyValues.Length == 1 && keyValues[0] is Guid guid && guid != Guid.Empty)
        {
            return guid.ToString();
        }

        return keyValues.Length == 1
            ? keyValues[0]?.ToString() ?? string.Empty
            : string.Join(":", keyValues.Select(v => v?.ToString() ?? string.Empty));
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Encrypted {Count} isolated properties for {EntityType}/{EntityId}")]
    private static partial void LogEncryptedProperties(
        ILogger logger, string entityType, string entityId, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Skipped encryption isolation for {EntityType}: entity ID is empty")]
    private static partial void LogSkippedNoId(ILogger logger, string entityType);
}
