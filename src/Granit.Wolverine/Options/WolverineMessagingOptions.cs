namespace Granit.Wolverine.Options;

/// <summary>
/// Configuration options for Granit Wolverine core (provider-agnostic).
/// </summary>
/// <remarks>
/// Bound from the <c>"Wolverine"</c> section of <c>appsettings.json</c>.
/// Does not contain any transport connection string — those live in provider-specific options
/// (e.g., <c>WolverinePostgresqlOptions</c>).
/// </remarks>
public sealed class WolverineMessagingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Wolverine";

    /// <summary>
    /// Cooldown delays between successive retry attempts.
    /// The number of elements determines the retry count per exception.
    /// Default: 5 s / 30 s / 5 min.
    /// </summary>
    public TimeSpan[] RetryDelays { get; set; } =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(5),
    ];

    /// <summary>
    /// Maximum number of retry attempts applied to <see cref="RetryDelays"/>.
    /// Must be ≥ 1. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// When <see langword="true"/>, the
    /// <see cref="Behaviors.TenantContextBehavior"/> rejects incoming envelopes
    /// that lack an <c>X-Tenant-Id</c> header unless the message type carries
    /// <see cref="CrossTenantMessageAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SECURITY: closes the cross-tenant code path that opens when a Wolverine
    /// handler runs without a tenant scope and falls through to
    /// <c>EfStoreBase</c>'s implicit cross-tenant query branch. Producer-side
    /// bugs (forgot to propagate the tenant) and forged envelopes both surface
    /// as <see cref="InvalidOperationException"/> instead of silent
    /// cross-tenant data access.
    /// </para>
    /// <para>
    /// Default: <see langword="false"/>. The metric
    /// <c>granit.wolverine.envelope.no_tenant</c> is emitted regardless, so SOC
    /// can quantify the migration burden before flipping the gate. Once the
    /// metric is steady at 0 (excluding events legitimately tagged
    /// <see cref="CrossTenantMessageAttribute"/>), the option can be set to
    /// <see langword="true"/> safely.
    /// </para>
    /// </remarks>
    public bool RequireEnvelopeTenant { get; set; }
}
