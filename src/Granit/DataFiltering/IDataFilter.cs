namespace Granit.DataFiltering;

/// <summary>
/// Service for runtime control of EF Core global query filters.
/// Allows selectively disabling a filter in the current async flow
/// without calling <c>IgnoreQueryFilters()</c> which disables all filters.
/// </summary>
/// <remarks>
/// Registered as Singleton. The state lives in a <c>static AsyncLocal</c> field
/// inside <see cref="DataFilter"/>, ensuring per-async-flow isolation.
/// </remarks>
public interface IDataFilter
{
    /// <summary>
    /// Disables the filter <typeparamref name="TFilter"/> for the current async context.
    /// Disposing the returned scope restores the previous state.
    /// </summary>
    /// <typeparam name="TFilter">
    /// Filter marker type (e.g. <c>ISoftDeletable</c>, <c>IMultiTenant</c>, <c>IActive</c>).
    /// </typeparam>
    /// <returns>Scope to dispose to restore the previous filter state.</returns>
    IDisposable Disable<TFilter>() where TFilter : class;

    /// <summary>
    /// Enables the filter <typeparamref name="TFilter"/> for the current async context.
    /// Useful for re-enabling a filter inside a scope that has disabled it.
    /// Disposing the returned scope restores the previous state.
    /// </summary>
    /// <typeparam name="TFilter">Filter marker type.</typeparam>
    /// <returns>Scope to dispose to restore the previous filter state.</returns>
    IDisposable Enable<TFilter>() where TFilter : class;

    /// <summary>
    /// Returns whether the filter <typeparamref name="TFilter"/> is currently enabled
    /// in the current async flow. Default state (no override active) is <c>true</c>.
    /// </summary>
    /// <typeparam name="TFilter">Filter marker type.</typeparam>
    bool IsEnabled<TFilter>() where TFilter : class;
}
