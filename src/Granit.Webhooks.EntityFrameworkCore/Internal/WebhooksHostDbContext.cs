using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// Host-pinned EF Core DbContext that backs every webhook subscription, signing key, and
/// delivery attempt whose physical placement is the host schema.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> this is
/// the single context that serves both host-managed and tenant-managed subscriptions; the
/// <c>MultiTenant</c> row-level filter enforces per-tenant isolation.
/// </para>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/> this
/// context holds <i>only</i> platform-managed subscriptions (<c>TenantId == null</c>) — SIEM
/// forwards, SOC2 — and tenant-managed subscriptions live in the companion
/// <see cref="WebhooksTenantDbContext"/>.
/// </para>
/// <para>
/// Registered via <c>AddGranitDbContext&lt;WebhooksHostDbContext&gt;</c>. Tables live in
/// <see cref="GranitDbDefaults.HostDbSchema"/>.
/// </para>
/// </remarks>
internal sealed class WebhooksHostDbContext(
    DbContextOptions<WebhooksHostDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), IWebhooksDbContext
{
    /// <inheritdoc/>
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    /// <inheritdoc/>
    public DbSet<WebhookSigningKey> WebhookSigningKeys => Set<WebhookSigningKey>();

    /// <inheritdoc/>
    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureWebhooksModule();
}
