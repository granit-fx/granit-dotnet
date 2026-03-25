using Granit.Localization.EntityFrameworkCore.Entities;
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
    IDbContextFactory<LocalizationDbContext> contextFactory) : ILocalizationOverrideStoreReader, ILocalizationOverrideStoreWriter
{
    private readonly IDbContextFactory<LocalizationDbContext> _contextFactory = contextFactory;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetOverridesAsync(
        string resourceName, string culture, CancellationToken cancellationToken = default)
    {
        await using LocalizationDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<LocalizationOverride> rows = await context.LocalizationOverrides
            .AsNoTracking()
            .Where(o => o.ResourceName == resourceName && o.CultureName == culture)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(o => o.Key, o => o.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task SetOverrideAsync(
        string resourceName, string culture, string key, string value, CancellationToken cancellationToken = default)
    {
        await using LocalizationDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        LocalizationOverride? existing = await context.LocalizationOverrides
            .FirstOrDefaultAsync(
                o => o.ResourceName == resourceName && o.CultureName == culture && o.Key == key,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            context.LocalizationOverrides.Add(new LocalizationOverride
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveOverrideAsync(
        string resourceName, string culture, string key, CancellationToken cancellationToken = default)
    {
        await using LocalizationDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        LocalizationOverride? existing = await context.LocalizationOverrides
            .FirstOrDefaultAsync(
                o => o.ResourceName == resourceName && o.CultureName == culture && o.Key == key,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        context.LocalizationOverrides.Remove(existing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
