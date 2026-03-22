using System.Collections.Concurrent;
using System.Reflection;
using Granit.Encryption.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Encryption.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core <see cref="IMaterializationInterceptor"/> that decrypts properties
/// marked with <c>[Encrypted(KeyIsolation = true)]</c> after entity materialization,
/// using a per-entity key from <see cref="IEntityEncryptionKeyStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the per-entity key has been destroyed (crypto-shredding), the property value
/// is set to <c>null</c> for nullable strings or <c>string.Empty</c> for non-nullable strings.
/// The raw ciphertext is not exposed.
/// </para>
/// <para>
/// Uses <see cref="Task.GetAwaiter()"/> + <see cref="System.Runtime.CompilerServices.TaskAwaiter.GetResult()"/>
/// because <see cref="IMaterializationInterceptor.InitializedInstance"/> is synchronous.
/// The <see cref="IEntityEncryptionKeyStore"/> implementation should use an in-memory
/// cache to minimize blocking calls.
/// </para>
/// </remarks>
public sealed partial class EncryptionIsolationMaterializationInterceptor(
    IEntityEncryptionKeyStore keyStore,
    ILogger<EncryptionIsolationMaterializationInterceptor> logger) : IMaterializationInterceptor
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> IsolatedPropertiesCache = new();

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        Type entityType = entity.GetType();
        PropertyInfo[] isolatedProperties = GetIsolatedProperties(entityType);

        if (isolatedProperties.Length == 0)
        {
            return entity;
        }

        string entityTypeName = entityType.Name;
        string entityId = GetEntityId(entity, entityType);

        if (string.IsNullOrEmpty(entityId))
        {
            return entity;
        }

        // Synchronous call — IEntityEncryptionKeyStore implementations should cache keys
        byte[]? key = keyStore.GetKeyAsync(entityTypeName, entityId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (key is null)
        {
            // Key was shredded — clear all isolated properties
            foreach (PropertyInfo property in isolatedProperties)
            {
                NullableContextInfoProvider info = new(property);
                property.SetValue(entity, info.IsNullable ? null : string.Empty);
            }

            LogShreddedEntity(logger, entityTypeName, entityId);
            return entity;
        }

        foreach (PropertyInfo property in isolatedProperties)
        {
            string? cipherText = property.GetValue(entity) as string;
            if (cipherText is null)
            {
                continue;
            }

            string? plainText = IsolatedFieldEncryptor.Decrypt(key, cipherText);
            property.SetValue(entity, plainText);
        }

        return entity;
    }

    private static PropertyInfo[] GetIsolatedProperties(Type entityType) =>
        IsolatedPropertiesCache.GetOrAdd(entityType, static type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static p =>
                    p.PropertyType == typeof(string) &&
                    p.GetCustomAttribute<EncryptedAttribute>() is { KeyIsolation: true })
                .ToArray());

    private static string GetEntityId(object entity, Type entityType)
    {
        // Convention: entities have an 'Id' property
        PropertyInfo? idProperty = entityType.GetProperty("Id");
        object? idValue = idProperty?.GetValue(entity);

        if (idValue is Guid guid && guid != Guid.Empty)
        {
            return guid.ToString();
        }

        return idValue?.ToString() ?? string.Empty;
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Entity {EntityType}/{EntityId} has been crypto-shredded — isolated properties cleared")]
    private static partial void LogShreddedEntity(ILogger logger, string entityType, string entityId);

    /// <summary>Helper to check property nullability via NullableContextAttribute.</summary>
    private readonly struct NullableContextInfoProvider
    {
        public bool IsNullable { get; }

        public NullableContextInfoProvider(PropertyInfo property)
        {
            NullabilityInfoContext context = new();
            NullabilityInfo info = context.Create(property);
            IsNullable = info.ReadState is NullabilityState.Nullable;
        }
    }
}
