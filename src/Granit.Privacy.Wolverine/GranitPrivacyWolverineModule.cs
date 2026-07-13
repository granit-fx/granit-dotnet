using Granit.Modularity;
using Granit.Wolverine;

namespace Granit.Privacy.Wolverine;

/// <summary>
/// Hosts the Wolverine sagas and handlers for the privacy module: the scatter-gather
/// personal-data export saga, the deferred deletion saga, and the legal-document cache
/// invalidation handler. All are discovered by Wolverine's assembly scanning — loading this
/// module puts the assembly in <c>ModuleAssemblies</c>, which
/// <c>AddGranitWolverine</c> feeds to <c>opts.Discovery.IncludeAssembly</c>; no explicit
/// registration is needed.
/// </summary>
/// <remarks>
/// Separated from the base <c>Granit.Privacy</c> package so the domain layer carries no
/// <c>WolverineFx</c> dependency (module anatomy: messaging adapters live in
/// <c>Granit.{Module}.Wolverine</c>). This also retires the <c>PrivateAssets="analyzers"</c>
/// source-generator workaround the base package needed while it declared sagas (#2540).
/// </remarks>
[DependsOn(typeof(GranitPrivacyModule))]
[DependsOn(typeof(GranitWolverineModule))]
public sealed class GranitPrivacyWolverineModule : GranitModule;
