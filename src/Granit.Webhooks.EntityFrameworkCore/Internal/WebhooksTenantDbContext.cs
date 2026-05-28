using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// Tenant-isolated EF Core DbContext used under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>. Holds
/// tenant-managed webhook subscriptions together with their signing keys and delivery
/// attempts. Each tenant's data lives in its own schema (under <c>SchemaPerTenant</c>) or
/// database (under <c>DatabasePerTenant</c>) — <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c>
/// lessivage is the GDPR Art. 17 right-of-erasure primitive enabled by this layout.
/// </summary>
/// <remarks>
/// Registered via <c>AddGranitIsolatedDbContext&lt;WebhooksTenantDbContext&gt;</c>. The
/// companion host-side context is <see cref="WebhooksHostDbContext"/>.
/// </remarks>
internal sealed class WebhooksTenantDbContext(
    DbContextOptions<WebhooksTenantDbContext> options,
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
