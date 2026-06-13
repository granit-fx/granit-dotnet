namespace Granit.Identity;

/// <summary>
/// Durable store for device-trust verdicts, keyed by <c>(userId, deviceId)</c>. The single source of
/// truth that the canonical <c>/devices</c> surface and the step-up decision read device trust from.
/// </summary>
/// <remarks>
/// <para>
/// The default registration (<c>Granit.Identity.Abstractions</c>) is an in-memory, single-node,
/// non-durable store suitable for development. Install <c>Granit.Identity.EntityFrameworkCore</c> for a
/// durable store that survives restarts and is shared across instances.
/// </para>
/// <para>
/// Lifetime contract: implementations must be safe to resolve per request scope. The default in-memory
/// store is registered <c>Singleton</c> (shared state) while the EF Core store is <c>Scoped</c>; consumers
/// must therefore depend on this interface from a scoped (or transient) service — never capture it in a
/// singleton.
/// </para>
/// </remarks>
public interface IDeviceTrustStore
{
    /// <summary>Stores (or replaces) the trust verdict for a device.</summary>
    Task SetAsync(
        string userId,
        string deviceId,
        DeviceTrustVerdict verdict,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the verdict for a single device, or <see langword="null"/> when none has been recorded.</summary>
    Task<DeviceTrustVerdict?> GetAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads verdicts for many devices of a user in one call (for list endpoints). Devices without a
    /// recorded verdict are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, DeviceTrustVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> deviceIds,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the trust verdict for a device (revoke trust). A no-op when none exists.</summary>
    Task RevokeAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);
}
