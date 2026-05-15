namespace Granit.DataFiltering;

/// <summary>
/// Null Object implementation of <see cref="IDataFilter"/>: every filter is reported as
/// enabled and <see cref="Disable{TFilter}"/> / <see cref="Enable{TFilter}"/> return a no-op
/// scope. Useful for design-time <c>IDesignTimeDbContextFactory</c> implementations and any
/// code path that needs to construct a Granit-aware <c>DbContext</c> without a DI container.
/// </summary>
/// <remarks>
/// Passing <see cref="Instance"/> alongside <see cref="MultiTenancy.NullTenantContext.Instance"/>
/// to <c>ApplyGranitConventions</c> ensures the design-time model snapshot encodes exactly the
/// same named query filters as the runtime model — preventing EF Core 10's
/// <c>PendingModelChangesWarning</c> from firing at app startup when the snapshot was generated
/// with <c>null</c> services.
/// </remarks>
public sealed class NullDataFilter : IDataFilter
{
    /// <summary>Canonical singleton instance — the type is stateless.</summary>
    public static readonly NullDataFilter Instance = new();

    private NullDataFilter() { }

    /// <inheritdoc/>
    public IDisposable Disable<TFilter>() where TFilter : class => NullScope.Value;

    /// <inheritdoc/>
    public IDisposable Enable<TFilter>() where TFilter : class => NullScope.Value;

    /// <inheritdoc/>
    public bool IsEnabled<TFilter>() where TFilter : class => true;

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Value = new();
        public void Dispose() { }
    }
}
