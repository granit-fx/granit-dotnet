using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="UserNotification"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all user notifications are returned cross-tenant.
/// </summary>
internal sealed class EfUserNotificationQueryableSource(
    IDbContextFactory<NotificationsDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<UserNotification>, IAsyncDisposable, IDisposable
{
    private NotificationsDbContext? _context;

    public IQueryable<UserNotification> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<UserNotification> query = _context.UserNotifications.AsNoTracking();
        return scope.Restrict(query, typeof(UserNotification).Name);
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
