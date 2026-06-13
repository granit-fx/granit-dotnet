using Granit.QueryEngine;
using Granit.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="SettingRecord"/>,
/// backing <c>MapGranitQuery&lt;SettingRecord&gt;</c> and the analytics runner over <c>SettingRecordQuery</c>.
/// </summary>
/// <remarks>
/// <see cref="SettingRecord"/> is not <c>IMultiTenant</c> (settings are scoped by the
/// provider/key columns, not a tenant filter), so no query-filter bypass is required.
/// </remarks>
internal sealed class EfSettingRecordQueryableSource(
    IDbContextFactory<SettingsDbContext> contextFactory)
    : IQueryableSource<SettingRecord>, IAsyncDisposable, IDisposable
{
    private SettingsDbContext? _context;

    public IQueryable<SettingRecord> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        return _context.SettingRecords.AsNoTracking();
    }

    public ValueTask DisposeAsync()
    {
        SettingsDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
