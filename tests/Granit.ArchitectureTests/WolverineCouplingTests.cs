using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Guard-rail: domain modules must not directly reference Wolverine.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class WolverineCouplingTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(WolverineCouplingTests).Assembly);

    /// <summary>
    /// Projects whose direct Wolverine coupling is legitimate and part of their public contract.
    /// </summary>
    private static readonly HashSet<string> WolverineAdapterProjects = new(StringComparer.Ordinal)
    {
        "Granit.Wolverine",
        "Granit.Wolverine.Postgresql",
        "Granit.Wolverine.SqlServer",
        "Granit.Events.Wolverine",
        "Granit.BackgroundJobs.Wolverine",
        "Granit.Scheduling.Wolverine",
        "Granit.Webhooks.Wolverine",
        "Granit.Notifications.Wolverine",
        "Granit.Presence.Wolverine",
        "Granit.Privacy.BackgroundJobs.Wolverine",
        "Granit.Scheduling.BackgroundJobs",
        // Granit.Privacy's *Eto contracts carry [SagaIdentity] (correlation member RequestId
        // matches no Wolverine naming convention) and are published by a dozen provider
        // packages, so the attribute reference cannot move. The sagas/handlers themselves
        // live in Granit.Privacy.Wolverine.
        "Granit.Privacy",
        // Hosts the export/deletion sagas + legal-document cache invalidation handler.
        "Granit.Privacy.Wolverine",
        "Granit.Wolverine.Encryption",
    };

    [Fact]
    public void Domain_packages_should_not_use_Wolverine_namespace() =>
        WolverineCouplingRules.DomainPackagesShouldNotUseWolverineNamespace(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            WolverineAdapterProjects);

    [Fact]
    public void Domain_packages_should_not_reference_WolverineFx_package() =>
        WolverineCouplingRules.DomainPackagesShouldNotReferenceWolverineFxPackage(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            WolverineAdapterProjects);
}
