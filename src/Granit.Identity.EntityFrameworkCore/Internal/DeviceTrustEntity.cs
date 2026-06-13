namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity persisting a <see cref="DeviceTrustVerdict"/> for a device, keyed by
/// <c>(UserId, DeviceId)</c>.
/// </summary>
internal sealed class DeviceTrustEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Subject the device belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Stable device identifier.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Trust level. Persisted as its PascalCase name (for SQL-audit readability) by the Granit
    /// enum-as-string convention applied via <c>ApplyGranitConventions</c>.
    /// </summary>
    public DeviceTrustLevel Level { get; set; }

    /// <summary>When trust was established.</summary>
    public DateTimeOffset TrustedAt { get; set; }

    /// <summary>When trust expires; <see langword="null"/> means no explicit expiry.</summary>
    public DateTimeOffset? TrustedUntil { get; set; }

    /// <summary>Audit-readable reason trust was granted.</summary>
    public string? Reason { get; set; }
}
