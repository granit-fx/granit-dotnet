using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Settings.Domain;
using Granit.Settings.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core <see cref="DbContext"/> for the Settings module — supersedes the
/// legacy interface-only pattern (host folding <c>ISettingsDbContext</c> into its own
/// <see cref="DbContext"/>).
/// </summary>
/// <remarks>
/// <para>
/// Scoping is handled by <see cref="SettingRecord.ProviderName"/> + <c>ProviderKey</c>
/// (<c>G</c>=Global, <c>T</c>=Tenant, <c>U</c>=User), not by the <c>IMultiTenant</c>
/// row-level filter — <see cref="SettingRecord"/> intentionally does not implement
/// <c>IMultiTenant</c>.
/// </para>
/// <para>
/// Host migrations: the dedicated context is <c>internal sealed</c>; consumers that need
/// to drive migrations from their own <see cref="DbContext"/> (CLAUDE.md "Framework
/// packages NEVER ship EF migrations") use the public
/// <see cref="ModelBuilderExtensions.ConfigureSettingsModule"/> to fold the model into
/// their migration-owning context.
/// </para>
/// </remarks>
internal sealed class SettingsDbContext(
    DbContextOptions<SettingsDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Setting records table.</summary>
    public DbSet<SettingRecord> SettingRecords => Set<SettingRecord>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureSettingsModule();
}
