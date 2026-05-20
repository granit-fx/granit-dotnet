namespace Granit.Http.SecurityHeaders;

/// <summary>
/// Singleton registry populated by <c>UseGranit*</c> extensions during
/// application configuration. Iterated read-only by the CSP composer at
/// response-emission time.
/// </summary>
/// <remarks>
/// The registry locks itself on the first call to its read surface from the
/// composer. Any subsequent <see cref="Add(ICspContributor)"/> call throws
/// <see cref="InvalidOperationException"/> with an actionable message — the
/// composer's per-endpoint cache is already warming at that point, and a
/// late-registered contributor would never be consulted, masking the bug.
/// </remarks>
public interface ICspContributorRegistry
{
    /// <summary>
    /// Registers a contributor. Idempotent on reference equality
    /// (re-registering the same instance is a no-op, not a duplicate).
    /// </summary>
    /// <param name="contributor">The contributor to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contributor"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The registry has already been locked by the composer. Register
    /// contributors during application configuration
    /// (e.g. inside <c>UseGranit*</c>) before the first request is served.
    /// </exception>
    void Add(ICspContributor contributor);

    /// <summary>
    /// Snapshot of registered contributors, in registration order.
    /// Locks the registry on first access.
    /// </summary>
    IReadOnlyCollection<ICspContributor> Contributors { get; }
}
