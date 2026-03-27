namespace Granit.RateLimiting.Options;

/// <summary>
/// Key partitioning strategy for rate limiting counters.
/// Determines how requests are grouped for quota enforcement.
/// </summary>
public enum RateLimitPartition : byte
{
    /// <summary>Partition by tenant only. All users of a tenant share the same quota.</summary>
    Tenant = 0,

    /// <summary>Partition by tenant and client IP. Each IP gets its own quota within a tenant.</summary>
    TenantAndIp = 1,

    /// <summary>Partition by client IP only. Recommended for unauthenticated endpoints (login, password reset).</summary>
    Ip = 2,

    /// <summary>Partition by authenticated user. Each user gets their own quota.</summary>
    User = 3,

    /// <summary>Partition by tenant and authenticated user. Each user gets their own quota within a tenant.</summary>
    TenantAndUser = 4,
}
