namespace Granit.Entities.Actions.Execution;

/// <summary>
/// One per-row failure reported by a bulk action run. UX-friendly partial
/// success — the renderer can highlight the failed rows and let the user
/// retry against them.
/// </summary>
/// <param name="Id">Primary key of the row that was rejected.</param>
/// <param name="Reason">Human-readable failure reason (e.g. <c>"NotFound"</c>, <c>"InvariantViolated"</c>, <c>"AccessDenied"</c>). Free-form on purpose — executors localise as needed.</param>
public sealed record BulkActionFailure(Guid Id, string Reason);
