using Granit.Localization.EntityFrameworkCore.Entities;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Localization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ILocalizationOverrideStoreReader"/> and <see cref="ILocalizationOverrideStoreWriter"/>.
/// Persists translation overrides in PostgreSQL (table <c>i18n_localization_overrides</c>)
/// with ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// Registered as a keyed service (<c>"efcore-raw"</c>) and wrapped by
/// <c>CachedLocalizationOverrideStore</c> which is the default reader/writer.
/// Each operation uses <see cref="IDbContextFactory{TContext}"/> to create and dispose its own
/// <see cref="LocalizationDbContext"/>, making it safe for concurrent request handling.
/// </remarks>
internal sealed class EfCoreLocalizationOverrideStore(
    IDbContextFactory<LocalizationDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<LocalizationOverride, LocalizationDbContext>(contextFactory, currentTenant), ILocalizationOverrideStoreReader, ILocalizationOverrideStoreWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetOverridesAsync(
        string resourceName, string culture, CancellationToken cancellationToken = default)
    {
        List<LocalizationOverride> rows = await ReadAsync(
            async db => await db.LocalizationOverrides
                .AsNoTracking()
                .Where(o => o.ResourceName == resourceName && o.CultureName == culture)
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(o => o.Key, o => o.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public Task SetOverrideAsync(
        string resourceName, string culture, string key, string value, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            LocalizationOverride? existing = await db.LocalizationOverrides
                .FirstOrDefaultAsync(
                    o => o.ResourceName == resourceName && o.CultureName == culture && o.Key == key,
                    cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                db.LocalizationOverrides.Add(new LocalizationOverride
                {
                    ResourceName = resourceName,
                    CultureName = culture,
                    Key = key,
                    Value = value,
                });
            }
            else
            {
                existing.Value = value;
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveOverrideAsync(
        string resourceName, string culture, string key, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            LocalizationOverride? existing = await db.LocalizationOverrides
                .FirstOrDefaultAsync(
                    o => o.ResourceName == resourceName && o.CultureName == culture && o.Key == key,
                    cancellationToken).ConfigureAwait(false);

            if (existing is not null)
            {
                db.LocalizationOverrides.Remove(existing);
            }
        }, cancellationToken);
}
