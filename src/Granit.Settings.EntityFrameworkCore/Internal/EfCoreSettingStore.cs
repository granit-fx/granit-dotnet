using Granit.Encryption;
using Granit.Settings.Definitions;
using Granit.Settings.Domain;
using Granit.Settings.Values;
using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ISettingStoreReader"/> and
/// <see cref="ISettingStoreWriter"/>. Persists setting values in the dedicated
/// <see cref="SettingsDbContext"/> (table <c>settings_setting_records</c>) with ISO 27001
/// audit trail via the standard Granit auto-interceptors.
/// </summary>
/// <remarks>
/// Scoped: opens a fresh <see cref="SettingsDbContext"/> per operation via the registered
/// <see cref="IDbContextFactory{TContext}"/> — thread-safe for concurrent reads/writes.
/// </remarks>
internal sealed class EfCoreSettingStore(
    IDbContextFactory<SettingsDbContext> contextFactory,
    SettingDefinitionManager definitions,
    IStringEncryptionService encryption) : ISettingStoreReader, ISettingStoreWriter
{
    /// <inheritdoc/>
    public async Task<SettingValue?> GetOrNullAsync(
        string name,
        string providerName,
        string? providerKey,
        CancellationToken cancellationToken = default)
    {
        await using SettingsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

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
        if (value is not null && definitions.GetOrNull(name) is { IsEncrypted: true })
        {
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
        await using SettingsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<SettingRecord> records = await context.SettingRecords
            .AsNoTracking()
            .Where(r => r.ProviderName == providerName && r.ProviderKey == providerKey)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<SettingValue> result = new(records.Count);

        foreach (SettingRecord r in records)
        {
            string? value = r.Value;
            if (value is not null && definitions.GetOrNull(r.Name) is { IsEncrypted: true })
            {
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
        await using SettingsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        string? persistedValue = value;
        if (persistedValue is not null && definitions.GetOrNull(name) is { IsEncrypted: true })
        {
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
        await using SettingsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

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
