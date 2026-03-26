using Wolverine;

namespace Granit.Wolverine.Internal;

/// <summary>
/// Holds a reference to the <see cref="WolverineOptions"/> instance created during
/// <c>UseWolverine()</c>, allowing provider modules (PostgreSQL, SqlServer) to apply
/// configuration directly on the options while the service collection is still writable.
/// </summary>
/// <remarks>
/// As of Wolverine 3.0, <c>ConfigureWolverine()</c> registers deferred extensions
/// (<c>LambdaWolverineExtension</c>) in the IoC container. When Wolverine processes
/// these extensions during DI resolution, the service collection is already read-only,
/// causing <see cref="InvalidOperationException"/> for extensions that register services
/// (e.g., <c>PersistMessagesWithPostgresql()</c>).
/// <para>
/// This holder allows provider modules to call extension methods like
/// <c>PersistMessagesWithPostgresql()</c> during their <c>ConfigureServices</c> phase
/// (Phase 1), when the service collection is still mutable.
/// </para>
/// </remarks>
internal sealed class WolverineOptionsHolder(WolverineOptions options)
{
    public WolverineOptions Options { get; } = options;
}
