namespace Granit.Auditing.Domain;

/// <summary>
/// Controls how audit log entries are persisted after capture.
/// </summary>
public enum AuditPersistenceMode
{
    /// <summary>
    /// Entries are persisted synchronously within the interceptor's <c>SavedChangesAsync</c>
    /// callback. Guarantees durability at the cost of added latency.
    /// Required for ISO 27001 strict compliance (banking, HDS).
    /// </summary>
    /// <remarks>
    /// Secure by default: this is the zero value so an unconfigured
    /// <c>AuditingOptions.PersistenceMode</c> never silently
    /// falls back to a lossy mode. Opt into <see cref="Async"/> explicitly for throughput.
    /// </remarks>
    Strict,

    /// <summary>
    /// Entries are published to a <c>Channel&lt;T&gt;</c> and persisted asynchronously
    /// by a background worker. Best performance, but buffered entries may be lost on an
    /// ungraceful crash (the graceful-shutdown drain only covers SIGTERM, not SIGKILL).
    /// </summary>
    Async,
}
