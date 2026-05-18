using Granit.DataFiltering;
using Granit.Localization.Domain;
using Granit.Localization.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Localization.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit localization overrides.
/// </summary>
/// <remarks>
/// <para>
/// Isolated from the host application's DbContext to avoid coupling.
/// Stores only <see cref="LocalizationOverride"/> records — the base translations
/// live in embedded JSON files resolved by <c>JsonStringLocalizer</c>.
/// </para>
/// <para>
/// Compatible with PostgreSQL (ISO 27001 compliant).
/// </para>
/// </remarks>
internal sealed class LocalizationDbContext(
    DbContextOptions<LocalizationDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Translation overrides indexed by resource, culture, and key.</summary>
    public DbSet<LocalizationOverride> LocalizationOverrides { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureLocalizationModule();
}
