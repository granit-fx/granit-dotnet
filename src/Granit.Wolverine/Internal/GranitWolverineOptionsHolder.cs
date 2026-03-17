using Wolverine;

namespace Granit.Wolverine.Internal;

/// <summary>
/// Internal marker registered by <c>AddGranitWolverine()</c> so that
/// <c>AddGranitWolverineWithPostgresql()</c> can retrieve the live
/// <see cref="WolverineOptions"/> instance before the DI container is built.
/// </summary>
/// <remarks>
/// Wolverine 5.20+ registers <see cref="WolverineOptions"/> via a factory that
/// requires a built <see cref="IServiceProvider"/>, making it impossible to retrieve
/// the instance through the service collection before the container is built.
/// This holder is registered as an <c>ImplementationInstance</c> singleton — safe to
/// read via <see cref="ServiceDescriptor.ImplementationInstance"/> at any time.
/// </remarks>
internal sealed class GranitWolverineOptionsHolder(WolverineOptions options)
{
    internal WolverineOptions Options { get; } = options;
}
