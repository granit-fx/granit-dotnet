using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.MobilePush.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for mobile push device token persistence.
/// </summary>
internal sealed class MobilePushDbContext(
    DbContextOptions<MobilePushDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    private readonly IStringEncryptionService _encryption = encryption;

    /// <summary>Mobile push device tokens (FCM/APNs).</summary>
    public DbSet<MobilePushToken> MobilePushTokens => Set<MobilePushToken>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureMobilePushModule();
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
