using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="NotificationPreference"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all notification preferences are returned cross-tenant.
/// </summary>
internal sealed class EfNotificationPreferenceQueryableSource(
    IDbContextFactory<NotificationsDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<NotificationPreference>, IAsyncDisposable, IDisposable
{
    private NotificationsDbContext? _context;

    public IQueryable<NotificationPreference> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<NotificationPreference> query = _context.Preferences.AsNoTracking();
        return scope.Restrict(query, typeof(NotificationPreference).Name);
    }

    public ValueTask DisposeAsync()
    {
        NotificationsDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
