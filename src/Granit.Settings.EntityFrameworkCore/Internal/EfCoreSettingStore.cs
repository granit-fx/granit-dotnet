using Granit.Encryption;
using Granit.Settings.Definitions;
using Granit.Settings.EntityFrameworkCore.Entities;
using Granit.Settings.Values;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ISettingStoreReader"/> and <see cref="ISettingStoreWriter"/>.
/// Persists setting values in the host application's DbContext
/// (table <c>settings_setting_records</c>) with ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a Singleton. Uses <see cref="IServiceScopeFactory"/> to create
/// a dedicated scope per database operation, ensuring thread safety and correct
/// EF Core Scoped context lifetime.
/// </para>
/// <para>
/// The host DbContext must implement <see cref="ISettingsDbContext"/> and have
/// <c>AuditedEntityInterceptor</c> wired in its factory for ISO 27001 audit trail compliance.
/// </para>
/// </remarks>
internal sealed class EfCoreSettingStore<TDbContext>(
    IServiceScopeFactory scopeFactory,
    SettingDefinitionManager definitions) : ISettingStoreReader, ISettingStoreWriter
    where TDbContext : DbContext, ISettingsDbContext
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly SettingDefinitionManager _definitions = definitions;

    /// <inheritdoc/>
    public async Task<SettingValue?> GetOrNullAsync(
        string name,
        string providerName,
        string? providerKey,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        SettingRecord? record = await context.SettingRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Name == name && r.ProviderName == providerName && r.ProviderKey == providerKey,
                cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        string? value = record.Value;
        if (value is not null && _definitions.GetOrNull(name) is { IsEncrypted: true })
        {
            IStringEncryptionService encryption = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
            value = encryption.Decrypt(value);
        }

        return new SettingValue(record.Name, record.ProviderName, record.ProviderKey, value);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingValue>> GetListAsync(
        string providerName,
        string? providerKey,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        List<SettingRecord> records = await context.SettingRecords
            .AsNoTracking()
            .Where(r => r.ProviderName == providerName && r.ProviderKey == providerKey)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        IStringEncryptionService? encryption = null;
        List<SettingValue> result = new(records.Count);

        foreach (SettingRecord r in records)
        {
            string? value = r.Value;
            if (value is not null && _definitions.GetOrNull(r.Name) is { IsEncrypted: true })
            {
                encryption ??= scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
                value = encryption.Decrypt(value);
            }

            result.Add(new SettingValue(r.Name, r.ProviderName, r.ProviderKey, value));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string name,
        string providerName,
        string? providerKey,
        string? value,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        string? persistedValue = value;
        if (persistedValue is not null && _definitions.GetOrNull(name) is { IsEncrypted: true })
        {
            IStringEncryptionService encryption = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
            persistedValue = encryption.Encrypt(persistedValue);
        }

        SettingRecord? existing = await context.SettingRecords
            .FirstOrDefaultAsync(
                r => r.Name == name && r.ProviderName == providerName && r.ProviderKey == providerKey,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            context.SettingRecords.Add(new SettingRecord
            {
                Name = name,
                ProviderName = providerName,
                ProviderKey = providerKey,
                Value = persistedValue,
            });
        }
        else
        {
            existing.Value = persistedValue;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string name,
        string providerName,
        string? providerKey,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        SettingRecord? existing = await context.SettingRecords
            .FirstOrDefaultAsync(
                r => r.Name == name && r.ProviderName == providerName && r.ProviderKey == providerKey,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        context.SettingRecords.Remove(existing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
