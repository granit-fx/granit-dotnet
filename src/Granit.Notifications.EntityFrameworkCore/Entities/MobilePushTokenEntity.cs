using Granit.Domain;
using Granit.Notifications.MobilePush;

namespace Granit.Notifications.EntityFrameworkCore.Entities;

/// <summary>
/// EF Core entity for mobile push device token persistence.
/// </summary>
public sealed class MobilePushTokenEntity : CreationAuditedEntity, IMultiTenant
{
    public string UserId { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public MobilePlatform Platform { get; set; }
    public Guid? TenantId { get; set; }

    /// <summary>Maps to <see cref="MobilePushTokenInfo"/>.</summary>
    internal MobilePushTokenInfo ToTokenInfo() => new()
    {
        UserId = UserId,
        DeviceToken = DeviceToken,
        Platform = Platform,
        TenantId = TenantId,
        CreatedAt = CreatedAt,
    };
}
