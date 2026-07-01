using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Notifications.WebPush.Domain;
using Granit.Notifications.WebPush.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.WebPush.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for browser push subscription persistence.
/// </summary>
internal sealed class WebPushDbContext(
    DbContextOptions<WebPushDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    private readonly IStringEncryptionService _encryption = encryption;

    /// <summary>W3C Web Push browser subscriptions.</summary>
    public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureWebPushModule();
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
