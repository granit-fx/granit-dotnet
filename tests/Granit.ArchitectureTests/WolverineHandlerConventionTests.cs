using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates Wolverine handler conventions.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class WolverineHandlerConventionTests
{
    [Fact]
    public void Assemblies_with_internal_handlers_must_expose_internals_to_WolverineHandlers() =>
        WolverineHandlerConventionRules.AssembliesWithInternalHandlersMustExposeInternalsToWolverineHandlers(
            typeof(WolverineHandlerConventionTests).Assembly,
            "Granit.");
}
