namespace Granit.Auditing.Domain;

/// <summary>
/// Controls how audit log entries are persisted after capture.
/// </summary>
public enum AuditPersistenceMode
{
    /// <summary>
    /// Entries are published to a <c>Channel&lt;T&gt;</c> and persisted asynchronously
    /// by a background worker. Best performance, but entries may be lost on crash.
    /// </summary>
    Async = 0,

    /// <summary>
    /// Entries are persisted synchronously within the interceptor's <c>SavedChangesAsync</c>
    /// callback. Guarantees durability at the cost of added latency.
    /// Required for ISO 27001 strict compliance (banking, HDS).
    /// </summary>
    Strict = 1,
}
