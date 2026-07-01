using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookDeliveryAttempt"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all delivery attempts are returned cross-tenant.
/// </summary>
internal sealed class EfWebhookDeliveryAttemptQueryableSource(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<WebhookDeliveryAttempt>, IAsyncDisposable, IDisposable
{
    private WebhooksDbContext? _context;

    public IQueryable<WebhookDeliveryAttempt> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<WebhookDeliveryAttempt> query = _context.WebhookDeliveryAttempts.AsNoTracking();
        return scope.Restrict(query, typeof(WebhookDeliveryAttempt).Name);
    }

    public ValueTask DisposeAsync()
    {
        WebhooksDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
